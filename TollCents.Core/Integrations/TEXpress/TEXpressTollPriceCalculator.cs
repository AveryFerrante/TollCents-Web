using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress.Entities;
using TollCents.Core.Integrations.TEXpress.Services;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressTollPriceCalculator
    {
        Task<TEXpressCalculationsResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag);
    }

    public class TEXpressTollPriceCalculator : ITEXpressTollPriceCalculator
    {
        private readonly string  _dataFilePath;
        private readonly double _tollAccessPointMatchToleranceMiles;
        private readonly double _noTollTagPriceMultiplier;
        private readonly bool _analysisModeEnabled = false;
        private readonly IMemoryCache _memoryCache;
        private readonly ITEXpressSegmentSkipAnomolies _texpressSegmentSkipAnomoliesService;
        private readonly ILogger<TEXpressTollPriceCalculator> _logger;
        private readonly IMediator? _mediator;

        public TEXpressTollPriceCalculator(IOptions<TEXpressIntegrationConfiguration> configuration,
            IMemoryCache memoryCache,
            ITEXpressSegmentSkipAnomolies texpressSegmentSkipAnomoliesService,
            ILogger<TEXpressTollPriceCalculator> logger,
            IMediator? mediator = null)
        {
            ArgumentNullException.ThrowIfNull(configuration?.Value);

            _dataFilePath = configuration.Value.MetadataFilePath;
            _tollAccessPointMatchToleranceMiles = configuration.Value.TollAccessPointMatchToleranceMiles;
            _noTollTagPriceMultiplier = configuration.Value.NoTollTagPriceMultiplier;
            _analysisModeEnabled = configuration.Value.AnalysisModeEnabled;
            _memoryCache = memoryCache;
            _texpressSegmentSkipAnomoliesService = texpressSegmentSkipAnomoliesService;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<TEXpressCalculationsResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag)
        {
            var numberedTollSteps = routeSteps
                .Where(IsTollStep)
                .Select((step, index) => new NumberedTollRouteStep { Step = step, StepNumber = index })
                .ToList();

            var numberedTEXpressSteps = numberedTollSteps.Where(a => IsTEXpressStep(a.Step)).ToList();

            if (!numberedTEXpressSteps.Any())
            {
                return new TEXpressCalculationsResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = true,
                    HasTollSteps = false
                };
            }

            var texpressSegments = await GetSegmentsAsync();
            if (!texpressSegments.Any())
            {
                _logger.LogNoTEXpressSegments(_dataFilePath);
                return new TEXpressCalculationsResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = false,
                    HasTollSteps = true
                };
            }

            _logger.LogInformation("Beginning processesing on {FoundSteps} found TEXpress steps", numberedTEXpressSteps.Count);
            bool matchedAllSegments = true;
            double totalTollPrice = 0;
            var matchedSegmentsMetadata = new List<MatchedSegmentMetadata>();
            numberedTEXpressSteps.ForEach(async currentNumberedStep =>
            {
                _logger.LogInformation("Analyzing TEXpress Step Number {StepNumber} | Description \"{StepDescription}\"",
                    currentNumberedStep.StepNumber,
                    currentNumberedStep.Step.NavigationInstruction.Instructions.Replace("\n", " "));
                await AnalyzeStepInitialInfo(currentNumberedStep);

                var currentStepNumber = currentNumberedStep.StepNumber;
                var currentTEXpressStep = currentNumberedStep.Step;
                var stepCardinalDirections = currentTEXpressStep.GetCardinalDirections();

                if (IsTakeRampStep(numberedTollSteps, currentNumberedStep))
                {
                    await AnalyzeStepEvent("Skipped Step Due to Ramp Step Match");
                    return;
                }

                if (_texpressSegmentSkipAnomoliesService.ShouldSkipSegmentAnalysis(numberedTollSteps, currentStepNumber))
                {
                    await AnalyzeStepEvent("Skipped Step Due to Segment Anomoly Match");
                    return;
                }

                // This may be unnecessary? Just use found start and end segments?
                var segmentMatchSet = GetAllStepSegments(currentTEXpressStep, texpressSegments);
                await AnalyzeStepEvent($"Matched Segments: {string.Join(", ", segmentMatchSet)}");

                var timeChoices = GetOrderedTEXpressPriceLookupKeys(routeSteps, currentStepNumber);

                var (startSegment, startEntryPoint) = GetStepStartSegment(texpressSegments.MatchesCardinalDirections(stepCardinalDirections),
                    currentTEXpressStep.StartLocation);
                await AnalyzeStepAccessEvent("Start", texpressSegments, currentNumberedStep, (startSegment, startEntryPoint));

                if (startSegment is null)
                {
                    _logger.LogWarning("Could not find start segment: Step Count {StepCount} | Description \"{StepDescription}\" | Start LatLng {StartLatLng}",
                        currentNumberedStep.StepNumber,
                        currentTEXpressStep.NavigationInstruction.Instructions.Replace("\n", " "),
                        JsonSerializer.Serialize(currentTEXpressStep.StartLocation.LatLng));
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched start segment: {SegmentDescription} | {EntryPoint}", startSegment!.Description, startEntryPoint?.Description);
                    double price = GetTollSegmentPrice(timeChoices, startSegment);
                    _logger.LogInformation("Adding price {Price} for this step", price);
                    if (price > 0)
                    {
                        totalTollPrice += price;
                        var skipWaypoints = startSegment.EntryPoints
                            .Where(ep => ep.SkipWaypoint is not null)
                            .Select(ep => ep.SkipWaypoint!.Value);
                        matchedSegmentsMetadata.Add(
                            new(startSegment.Description, startSegment.Identifier, price, skipWaypoints));
                    }
                    else matchedAllSegments = false;
                }

                if (EndsInSameSegment(currentTEXpressStep.EndLocation, startSegment))
                {
                    _logger.LogInformation("Step ends in same segment as start segment, skipping end segment price check.");
                    await AnalyzeStepEvent("End segment matched start segment, skipping end segment price check.");
                    return;
                }

                var (endSegment, endExitPoint) = GetStepEndSegment(texpressSegments.MatchesCardinalDirections(stepCardinalDirections),
                    currentTEXpressStep.EndLocation);
                await AnalyzeStepAccessEvent("End", texpressSegments, currentNumberedStep, (endSegment, endExitPoint));


                if (endSegment is null)
                {
                    _logger.LogWarning("Could not find end segment: Step Count {StepCount} | Description \"{StepDescription}\" | End LatLng {EndLatLng}",
                        currentNumberedStep.StepNumber,
                        currentTEXpressStep.NavigationInstruction.Instructions,
                        JsonSerializer.Serialize(currentTEXpressStep.EndLocation.LatLng));
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched end segment: {SegmentDescription}", endSegment.Description);
                    var price = GetTollSegmentPrice(timeChoices, endSegment);
                    _logger.LogInformation("Adding price {Price} for this step", price);
                    if (price > 0)
                    {
                        totalTollPrice += price;
                        var skipWaypoints = endSegment.EntryPoints
                            .Where(ep => ep.SkipWaypoint is not null)
                            .Select(ep => ep.SkipWaypoint!.Value);
                        matchedSegmentsMetadata.Add(
                            new(endSegment.Description, endSegment.Identifier, price, skipWaypoints));
                    }
                    else matchedAllSegments = false;
                }
            });

            var tollResponse = new TEXpressCalculationsResult
            {
                TotalTollPrice = hasTollTag ? totalTollPrice : (totalTollPrice * _noTollTagPriceMultiplier),
                MatchedAllSegments = matchedAllSegments,
                HasTollSteps = true,
                MatchedSegmentsMetadata = matchedSegmentsMetadata
            };

            _logger.LogInformation("Completed TEXpress toll price calculation. Total Price: {TotalPrice} | Matched All Segments: {MatchedAllSegments} | Has Toll Steps: {HasTollSteps}",
                tollResponse.TotalTollPrice,
                tollResponse.MatchedAllSegments,
                tollResponse.HasTollSteps);
            return tollResponse;
        }

        /// <summary>
        /// A single google maps step may span multiple TEXpress segments.
        /// This attempts to resolve all associated TEXpress segments.
        /// </summary>
        private IEnumerable<int> GetAllStepSegments(RouteLegStep step, IEnumerable<TEXpressSegment> texpressSegments)
        {
            var polylineCoords = step.Polyline.EncodedPolyline.Decode();
            var segmentMatchSet = new HashSet<int>();

            var cardinalDirections = polylineCoords.First().GetCardinalDirections(polylineCoords.Last());

            texpressSegments.MatchesCardinalDirections(cardinalDirections).ToList().ForEach(segment =>
            {
                var entryPoints = segment.EntryPoints.Select(ep => ep.Location);
                var match = polylineCoords.Any(coord => entryPoints.Any(point => point.DistanceToInMiles(coord) <= _tollAccessPointMatchToleranceMiles));
                if (match)
                {
                    segmentMatchSet.Add(segment.Identifier);
                }
            });

            return segmentMatchSet;
        }

        private async Task<IEnumerable<TEXpressSegment>> GetSegmentsAsync()
        {
            return await _memoryCache.GetOrCreateAsync("TEXpressSegments", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                var fileContent = await File.ReadAllTextAsync(_dataFilePath);
                var jsonOptions = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                var segments = JsonSerializer.Deserialize<IEnumerable<TEXpressSegment>>(fileContent, jsonOptions);
                return segments ?? Enumerable.Empty<TEXpressSegment>();
            }) ?? Enumerable.Empty<TEXpressSegment>();
        }

        private bool IsTakeRampStep(IEnumerable<NumberedTollRouteStep> tollSteps, NumberedTollRouteStep currentStep)
        {
            // If current step is a ramp step and the next step after is also a toll step,
            // it is a good indicator that this current step can be skipped for analysis.
            var immediateNextStep = tollSteps.FirstOrDefault(s => s.StepNumber == currentStep.StepNumber + 1);
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

        private (TEXpressSegment?, TollAccessPoint?) GetStepStartSegment(IEnumerable<TEXpressSegment> texpressSegments, RouteLocation stepLocation)
        {
            var stepStartLocation = stepLocation.ToCoordinate();
            var matchedSegment = texpressSegments
                .FirstOrDefault(segment => segment.EntryPoints
                    .Any(entryPoint => entryPoint.Location.DistanceToInMiles(stepStartLocation) <= _tollAccessPointMatchToleranceMiles));
            var entryPoint = matchedSegment?.EntryPoints.MinBy(point => point.Location.DistanceToInMiles(stepStartLocation));

            return (matchedSegment, entryPoint);
        }

        private (TEXpressSegment?, TollAccessPoint?) GetStepEndSegment(IEnumerable<TEXpressSegment> texpressSegments, RouteLocation stepLocation)
        {
            var stepStartLocation = stepLocation.ToCoordinate();
            var matchedSegment = texpressSegments
                .FirstOrDefault(segment => segment.ExitPoints
                    .Any(exitPoint => exitPoint.Location.DistanceToInMiles(stepStartLocation) <= _tollAccessPointMatchToleranceMiles));
            var exitPoint = matchedSegment?.ExitPoints.MinBy(point => point.Location.DistanceToInMiles(stepStartLocation));

            return (matchedSegment, exitPoint);
        }

        private bool IsTEXpressStep(RouteLegStep step) =>
            step.NavigationInstruction?.Instructions?.Contains("TEXPRESS", StringComparison.OrdinalIgnoreCase) ?? false;

        private bool IsTollStep(RouteLegStep step) => 
            step.NavigationInstruction?.Instructions?.Contains("TOLL ROAD", StringComparison.OrdinalIgnoreCase) ?? false;
        

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

        private async Task AnalyzeStepInitialInfo(NumberedTollRouteStep numberedStep)
        {
            await (_mediator?.Send(new StepInitialInfoEvent(
                EventMessage: "Initial Step Information",
                StepNumber: numberedStep.StepNumber,
                StepInstruction: numberedStep.Step.NavigationInstruction?.Instructions,
                Polyline: numberedStep.Step.Polyline?.EncodedPolyline,
                CardinalDirections: numberedStep.Step.GetCardinalDirections(),
                Maneuver: numberedStep.Step.NavigationInstruction?.Maneuver?.ToString()
            )) ?? Task.CompletedTask);
        }

        private async Task AnalyzeStepEvent(string message)
        {
            await (_mediator?.Send(new StepEvent(
                EventMessage: message
            )) ?? Task.CompletedTask);
        }

        private async Task AnalyzeStepAccessEvent(string accessPointType,
            IEnumerable<TEXpressSegment> texpressSegments,
            NumberedTollRouteStep currentNumberedStep,
            (TEXpressSegment? MatchedSegment, TollAccessPoint? MatchedAccessPoint) foundValue)
        {
            if (_mediator is null) return;

            var stepLatLong = accessPointType == "Start" ? currentNumberedStep.Step.StartLocation.ToCoordinate() :
                currentNumberedStep.Step.EndLocation.ToCoordinate();

            var selector = (TEXpressSegment? segment) => accessPointType == "Start" ? segment?.EntryPoints : segment?.ExitPoints;

            var actualClosestSegment = texpressSegments.MinBy(segment => selector(segment)
                .Min(accessPoint => accessPoint.Location.DistanceToInMiles(stepLatLong)));
            var actualClosestEntry = selector(actualClosestSegment).MinBy(entryPoint => entryPoint.Location.DistanceToInMiles(stepLatLong));


            await _mediator.Send(new StepTollAccessEvent(
                EventMessage: $"Step {accessPointType} Access Point Analysis",
                AccessType: accessPointType,
                StepLatLong: $"{stepLatLong.Latitude}, {stepLatLong.Longitude}",
                MatchedSegment: foundValue.MatchedSegment?.Description,
                MatchedAccessPoint: foundValue.MatchedAccessPoint?.Description,
                MatchedDistance: foundValue.MatchedAccessPoint?.Location.DistanceToInMiles(stepLatLong),
                ActualClosestSegment: actualClosestSegment?.Description,
                ActualClosestAccessPoint: actualClosestEntry?.Description,
                ActualClosestDistance: actualClosestEntry?.Location.DistanceToInMiles(stepLatLong),
                ActualClosestCardinalDirections: actualClosestSegment?.CardinalDirections
            ))!;

        }
    }

    internal class TEXpressPriceLookupChoice
    {
        public required string ChoiceTime { get; set; }
        public required string ChoiceDay { get; set; }
    }

    public class TEXpressCalculationsResult
    {
        public double TotalTollPrice { get; set; }

        public bool MatchedAllSegments { get; set; }

        public bool HasTollSteps { get; set; }

        public List<MatchedSegmentMetadata> MatchedSegmentsMetadata { get; set; } = new();
    }

    public record MatchedSegmentMetadata(
        string SegmentDescription,
        int SegmentIdentifier,
        double Price,
        IEnumerable<Coordinate> SkipWaypoints
    );

    public class NumberedTollRouteStep
    {
        public int StepNumber { get; set; }
        public required RouteLegStep Step { get; set; }
    }
}
