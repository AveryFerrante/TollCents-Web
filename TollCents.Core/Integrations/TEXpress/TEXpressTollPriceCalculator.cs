using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text.Json;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress.Entities;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressTollPriceCalculator
    {
        Task<TEXpressTollPriceResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag);
    }

    public class TEXpressTollPriceCalculator : ITEXpressTollPriceCalculator
    {
        private readonly string  _dataFilePath;
        private readonly double _tollAccessPointMatchToleranceMiles;
        private readonly double? _noTollTagPriceMultiplier;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<TEXpressTollPriceCalculator> _logger;

        public TEXpressTollPriceCalculator(IIntegrationsConfiguration configuration, IMemoryCache memoryCache, ILogger<TEXpressTollPriceCalculator> logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configuration?.Integrations?.TEXpressDataFilePath, nameof(
                configuration.Integrations.TEXpressDataFilePath));
            ArgumentNullException.ThrowIfNull(configuration?.Integrations?.TollAccessPointMatchToleranceMiles, nameof(
                configuration.Integrations.TollAccessPointMatchToleranceMiles));

            _dataFilePath = configuration.Integrations.TEXpressDataFilePath;
            _tollAccessPointMatchToleranceMiles = configuration.Integrations.TollAccessPointMatchToleranceMiles.Value;
            _noTollTagPriceMultiplier = configuration.Integrations.NoTollTagPriceMultiplier;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<TEXpressTollPriceResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag)
        {
            var numberedTEXpressSteps = routeSteps
                .Select((step, index) => new NumberedRouteStep { Step = step, StepNumber = index })
                .Where(a => IsTEXpressTollStep(a.Step)).ToList();
            if (!numberedTEXpressSteps.Any())
                return new TEXpressTollPriceResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = true,
                    HasTollSteps = false
                };

            var texpressSegments = await GetSegmentsAsync();
            if (!texpressSegments.Any()) 
                return new TEXpressTollPriceResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = false,
                    HasTollSteps = true
                };

            _logger.LogInformation("Beginning processesing on {FoundSteps} found TEXpress steps", numberedTEXpressSteps.Count);
            bool matchedAllSegments = true;
            double totalTollPrice = 0;
            numberedTEXpressSteps.ForEach(currentNumberedStep =>
            {
                _logger.LogInformation("Analyzing TEXpress Step Number {StepNumber} | Description \"{StepDescription}\"",
                    currentNumberedStep.StepNumber,
                    currentNumberedStep.Step.NavigationInstruction.Instructions);
                if (IsTakeRampStep(numberedTEXpressSteps, currentNumberedStep))
                {
                    return;
                }

                var timeChoices = GetOrderedTEXpressPriceLookupKeys(routeSteps, currentNumberedStep.StepNumber);
                var texpressStep = currentNumberedStep.Step;

                TEXpressSegment? startSegment = GetStepStartSegment(texpressSegments, texpressStep.StartLocation);

                if (startSegment is null)
                {
                    _logger.LogWarning("Could not find start segment: Step Count {StepCount} | Description \"{StepDescription}\" | Start LatLng {StartLatLng}",
                        currentNumberedStep.StepNumber,
                        texpressStep.NavigationInstruction.Instructions,
                        JsonSerializer.Serialize(texpressStep.StartLocation.LatLng));
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched start segment: {SegmentDescription}", startSegment.Description);
                    double price = GetTollSegmentPrice(timeChoices, startSegment);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;
                }

                if (EndsInSameSegment(texpressStep.EndLocation, startSegment))
                {
                    _logger.LogInformation("Step ends in same segment as start segment, skipping end segment price check.");
                    return;
                }

                TEXpressSegment? endSegment = GetStepEndSegment(texpressSegments, texpressStep.EndLocation);
                if (endSegment is null)
                {
                    _logger.LogWarning("Could not find end segment: Step Count {StepCount} | Description \"{StepDescription}\" | End LatLng {EndLatLng}",
                        currentNumberedStep.StepNumber,
                        texpressStep.NavigationInstruction.Instructions,
                        JsonSerializer.Serialize(texpressStep.EndLocation.LatLng));
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched end segment: {SegmentDescription}", endSegment.Description);
                    var price = GetTollSegmentPrice(timeChoices, endSegment);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;
                }
            });

            var tollResponse = new TEXpressTollPriceResult
            {
                TotalTollPrice = hasTollTag ? totalTollPrice : (totalTollPrice * (_noTollTagPriceMultiplier ?? 1)),
                MatchedAllSegments = matchedAllSegments,
                HasTollSteps = true
            };

            _logger.LogInformation("Completed TEXpress toll price calculation. Total Price: {TotalPrice} | Matched All Segments: {MatchedAllSegments} | Has Toll Steps: {HasTollSteps}",
                tollResponse.TotalTollPrice,
                tollResponse.MatchedAllSegments,
                tollResponse.HasTollSteps);

            return tollResponse;
        }

        private async Task<IEnumerable<TEXpressSegment>> GetSegmentsAsync()
        {
            return await _memoryCache.GetOrCreateAsync("TEXpressSegments", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                var fileContent = await File.ReadAllTextAsync(_dataFilePath);
                var segments = JsonSerializer.Deserialize<IEnumerable<TEXpressSegment>>(fileContent);
                return segments ?? Enumerable.Empty<TEXpressSegment>();
            }) ?? Enumerable.Empty<TEXpressSegment>();
        }

        private bool IsTakeRampStep(IEnumerable<NumberedRouteStep> steps, NumberedRouteStep currentStep)
        {
            // Back to back steps is a good starting indicator of a merging step
            var immediateNextStep = steps.FirstOrDefault(s => s.StepNumber == currentStep.StepNumber + 1);
            if (immediateNextStep is null) return false;

            if (currentStep.Step.NavigationInstruction.Maneuver == Maneuver.RampLeft ||
                currentStep.Step.NavigationInstruction.Maneuver == Maneuver.RampRight)
            {
                // TODO: Also check distance? Ramp steps *SHOULD* be short....
                _logger.LogInformation("Found IsRamp step for \"{StepDescription}\", Step Number {StepNumber}",
                    currentStep.Step.NavigationInstruction.Instructions,
                    currentStep.StepNumber);
                return true;
            }

            return false;
        }

        private bool EndsInSameSegment(RouteLocation stepEndLocation, TEXpressSegment? startSegment)
        {
            if (startSegment is null) return false;
            var stepEndLocationCoord = stepEndLocation.ToCoordinate();
            return startSegment.ExitPoints
                .Any(exitPoint => exitPoint.Location.DistanceToInMiles(stepEndLocationCoord) <= _tollAccessPointMatchToleranceMiles);
        }

        private static double GetTollSegmentPrice(IEnumerable<TEXpressPriceLookupChoice> timeChoices, TEXpressSegment tollSegment)
        {
            double price = 0;
            foreach (var choice in timeChoices)
            {
                var tollPrices = tollSegment.TimeOfDayPricing[choice.ChoiceDay];
                if (tollPrices is null) continue;
                price = tollPrices.FirstOrDefault(tp => tp.Time == choice.ChoiceTime).Price;
                if (price > 0) break;
            }
            return price;
        }

        private TEXpressSegment? GetStepStartSegment(IEnumerable<TEXpressSegment> texpressSegments, RouteLocation stepLocation)
        {
            var stepStartLocation = stepLocation.ToCoordinate();
            return texpressSegments
                .FirstOrDefault(segment => segment.EntryPoints
                    .Any(entryPoint => entryPoint.Location.DistanceToInMiles(stepStartLocation) <= _tollAccessPointMatchToleranceMiles));
        }

        private TEXpressSegment? GetStepEndSegment(IEnumerable<TEXpressSegment> texpressSegments, RouteLocation stepLocation)
        {
            var stepEndLocation = stepLocation.ToCoordinate();
            return texpressSegments
                .FirstOrDefault(segment => segment.ExitPoints
                    .Any(exitPoint => exitPoint.Location.DistanceToInMiles(stepEndLocation) <= _tollAccessPointMatchToleranceMiles));
        }

        private bool IsTEXpressTollStep(RouteLegStep step)
        {
            var isTollStep = (step.NavigationInstruction?.Instructions?.Contains("TOLL ROAD", StringComparison.OrdinalIgnoreCase) ?? false);
            var isTEXpressStep =
                (step.NavigationInstruction?.Instructions?.Contains("TEXPRESS", StringComparison.OrdinalIgnoreCase) ?? false) &&
                isTollStep;

            if (isTollStep)
            {
                _logger.LogInformation("Found toll step. Description \"{StepDescription}\" | isTEXpressStep: {IsTEXpressStep}",
                    step.NavigationInstruction?.Instructions,
                    isTEXpressStep.ToString());
            }

            return isTEXpressStep;
        }

        static TimeSpan GetTollStepArrivalTimeOffset(IEnumerable<RouteLegStep> routeSteps, int tollStepIndex)
        {
            var timeToToll = TimeSpan.Zero;
            if (tollStepIndex > 0)
            {
                timeToToll += routeSteps.Take(tollStepIndex - 1)
                    .Aggregate(timeToToll, (currentSum, incomingStep) => currentSum.Add(incomingStep.StaticDuration ?? TimeSpan.Zero));
            }

            return timeToToll;
        }

        private static List<TEXpressPriceLookupChoice> GetOrderedTEXpressPriceLookupKeys(IEnumerable<RouteLegStep> routeSteps, int tollStepIndex)
        {
            TimeSpan timeToTollArrival = GetTollStepArrivalTimeOffset(routeSteps, tollStepIndex);
            var cstZone = TimeZoneInfo.FindSystemTimeZoneById(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Central Standard Time" : "America/Chicago");
            var cstTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.Add(timeToTollArrival), cstZone);

            // Try nearest ":30" minute, round based on 15 minute intervals
            var firstTimeChoice = new DateTime(
                cstTime.Year, cstTime.Month, cstTime.Day,
                cstTime.Minute >= 45 ? cstTime.Hour + 1 : cstTime.Hour,
                cstTime.Minute < 15 || cstTime.Minute >= 45 ? 0 : 30,
                0
            );

            var secondTimeChoice = firstTimeChoice.Minute == 0 ? firstTimeChoice :
                new DateTime(cstTime.Year, cstTime.Month, cstTime.Day,
                cstTime.Minute >= 30 ? cstTime.Hour + 1 : cstTime.Hour, 0, 0);

            return new List<TEXpressPriceLookupChoice>
            {
                firstTimeChoice.ToPriceLookupChoice(),
                secondTimeChoice.ToPriceLookupChoice()
            };
        }
    }

    internal class TEXpressPriceLookupChoice
    {
        public required string ChoiceTime { get; set; }
        public required string ChoiceDay { get; set; }
    }

    public class TEXpressTollPriceResult
    {
        public double TotalTollPrice { get; set; }
        public bool MatchedAllSegments { get; set; }
        public bool HasTollSteps { get; set; }
    }

    public class NumberedRouteStep
    {
        public int StepNumber { get; set; }
        public required RouteLegStep Step { get; set; }
    }
}
