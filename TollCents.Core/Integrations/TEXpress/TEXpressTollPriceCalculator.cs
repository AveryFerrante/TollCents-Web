using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using Microsoft.Extensions.Caching.Memory;
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

        public TEXpressTollPriceCalculator(IIntegrationsConfiguration configuration, IMemoryCache memoryCache)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configuration?.Integrations?.TEXpressDataFilePath, nameof(
                configuration.Integrations.TEXpressDataFilePath));
            ArgumentNullException.ThrowIfNull(configuration?.Integrations?.TollAccessPointMatchToleranceMiles, nameof(
                configuration.Integrations.TollAccessPointMatchToleranceMiles));

            _dataFilePath = configuration.Integrations.TEXpressDataFilePath;
            _tollAccessPointMatchToleranceMiles = configuration.Integrations.TollAccessPointMatchToleranceMiles.Value;
            _noTollTagPriceMultiplier = configuration.Integrations.NoTollTagPriceMultiplier;
            _memoryCache = memoryCache;
        }

        public async Task<TEXpressTollPriceResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag)
        {
            var NumberedTEXpressSteps = routeSteps
                .Select((step, index) => new NumberedRouteStep { Step = step, StepNumber = index })
                .Where(a => IsTEXpressTollStep(a.Step)).ToList();
            if (!NumberedTEXpressSteps.Any())
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

            bool matchedAllSegments = true;
            double totalTollPrice = 0;
            NumberedTEXpressSteps.ForEach(currentNumberedStep =>
            {
                if (IsMergingStep(NumberedTEXpressSteps, currentNumberedStep))
                {
                    return;
                }

                var timeChoices = GetOrderedTEXpressPriceLookupKeys(routeSteps, currentNumberedStep.StepNumber);
                var texpressStep = currentNumberedStep.Step;

                TEXpressSegment? startSegment = GetStepStartSegment(texpressSegments, texpressStep.StartLocation);

                if (startSegment is null)
                {
                    matchedAllSegments = false;
                }
                else
                {
                    double price = GetTollSegmentPrice(timeChoices, startSegment);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;
                }

                if (EndsInSameSegment(texpressStep.EndLocation, startSegment))
                {
                    return;
                }

                TEXpressSegment? endSegment = GetStepEndSegment(texpressSegments, texpressStep.EndLocation);
                if (endSegment is null)
                {
                    matchedAllSegments = false;
                }
                else
                {
                    var price = GetTollSegmentPrice(timeChoices, endSegment);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;
                }
            });

            return new TEXpressTollPriceResult
            {
                TotalTollPrice = hasTollTag ? totalTollPrice : (totalTollPrice * (_noTollTagPriceMultiplier ?? 1)),
                MatchedAllSegments = matchedAllSegments,
                HasTollSteps = true
            };
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

        private bool IsMergingStep(IEnumerable<NumberedRouteStep> steps, NumberedRouteStep currentStep)
        {
            // Back to back steps is a good starting indicator of a merging step
            var immediateNextStep = steps.FirstOrDefault(s => s.StepNumber == currentStep.StepNumber + 1);
            if (immediateNextStep is null) return false;

            if (currentStep.Step.NavigationInstruction.Maneuver == Maneuver.RampLeft ||
                currentStep.Step.NavigationInstruction.Maneuver == Maneuver.RampRight)
            {
                // TODO: Also check distance? Ramp steps *SHOULD* be short....
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

        private static bool IsTEXpressTollStep(RouteLegStep step)
        {
            return
                (step.NavigationInstruction?.Instructions?.Contains("TEXPRESS", StringComparison.OrdinalIgnoreCase) ?? false) &&
                (step.NavigationInstruction?.Instructions?.Contains("TOLL ROAD", StringComparison.OrdinalIgnoreCase) ?? false);
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
