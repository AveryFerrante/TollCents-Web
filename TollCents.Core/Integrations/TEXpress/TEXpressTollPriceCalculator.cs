using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress.Entities;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressTollPriceCalculator
    {
        Task<TEXpressTollPriceResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag);
        Task<List<List<string>>> GetTEXpressTollPriceTest(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag);
        Task PrintPoints(Coordinate pointToMatch, string tollSegmentName, bool entryPoints = true);
    }

    public class TEXpressTollPriceCalculator : ITEXpressTollPriceCalculator
    {
        private readonly string  _dataFilePath;
        private readonly double _tollAccessPointMatchToleranceMiles;
        private readonly double _noTollTagPriceMultiplier;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<TEXpressTollPriceCalculator> _logger;
        private DebugLogProcessSummary _processSummary = new DebugLogProcessSummary();

        public TEXpressTollPriceCalculator(IIntegrationsConfiguration configuration, IMemoryCache memoryCache, ILogger<TEXpressTollPriceCalculator> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration.Integrations?.TEXpress,
                nameof(configuration.Integrations.TEXpress));
            var config = configuration.Integrations.TEXpress;

            _dataFilePath = config.MetadataFilePath;
            _tollAccessPointMatchToleranceMiles = config.TollAccessPointMatchToleranceMiles;
            _noTollTagPriceMultiplier = config.NoTollTagPriceMultiplier;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        /* New potential strategy:
         * Find first entry point into texpress
         * Follow all steps until no "Toll Road" steps remain
         * Concat all polylines into a single polyline
         * Have a list of geocoords along each texpress segment, check polyline coords against this list
         * Determine how many segments were traversed and calculate tolls based on that?
         */
        public async Task<List<List<string>>> GetTEXpressTollPriceTest(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag)
        {
            bool haveStartingPoint = false;
            List<List<string>> polylineGroups = new List<List<string>>();
            int curIndex = 0;
            routeSteps.ToList().ForEach(s =>
            {
                var isTexpressStep = IsTEXpressStep(s);
                if (isTexpressStep && !haveStartingPoint)
                {
                    haveStartingPoint = true;
                    polylineGroups.Add(new List<string> { s.Polyline.EncodedPolyline });
                }
                else if (isTexpressStep && haveStartingPoint)
                {
                    polylineGroups[curIndex].Add(s.Polyline.EncodedPolyline);

                }
                else if (!isTexpressStep && haveStartingPoint)
                {
                    haveStartingPoint = false;
                    curIndex++;
                }
            });
            return await Task.FromResult(polylineGroups);
        }

        public async Task<TEXpressTollPriceResult> GetTEXpressTollPrice(IEnumerable<RouteLegStep> routeSteps, bool hasTollTag)
        {
            var numberedTollSteps = routeSteps
                .Select((step, index) => new NumberedRouteStep { Step = step, StepNumber = index })
                .Where(a => IsTollStep(a.Step)).ToList();

            var numberedTEXpressSteps = numberedTollSteps.Where(a => IsTEXpressStep(a.Step)).ToList();

            if (!numberedTEXpressSteps.Any())
            {
                return new TEXpressTollPriceResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = true,
                    HasTollSteps = false
                };
            }

            var texpressSegments = await GetSegmentsAsync();
            if (!texpressSegments.Any())
            {
                return new TEXpressTollPriceResult
                {
                    TotalTollPrice = 0,
                    MatchedAllSegments = false,
                    HasTollSteps = true
                };
            }

            _logger.LogInformation("Beginning processesing on {FoundSteps} found TEXpress steps", numberedTEXpressSteps.Count);
            bool matchedAllSegments = true;
            double totalTollPrice = 0;
            List<Coordinate> skipWaypoints = new List<Coordinate>();
            numberedTEXpressSteps.ForEach(currentNumberedStep =>
            {
                _processSummary = new DebugLogProcessSummary();
                _logger.LogInformation("Analyzing TEXpress Step Number {StepNumber} | Description \"{StepDescription}\"",
                    currentNumberedStep.StepNumber,
                    currentNumberedStep.Step.NavigationInstruction.Instructions.Replace("\n", " "));

                var texpressStep = currentNumberedStep.Step;
                var cardinalDirections = texpressStep.StartLocation.ToCoordinate().GetCardinalDirection(texpressStep.EndLocation.ToCoordinate());
                _processSummary.StepCardinalDirection = string.Join(", ", cardinalDirections);
                _processSummary.StepPolyline = texpressStep.Polyline.EncodedPolyline;
                _processSummary.StepManeuver = texpressStep.NavigationInstruction.Maneuver.ToString();

                if (IsTakeRampStep(numberedTollSteps, currentNumberedStep))
                {
                    _processSummary.SkippedDueToRampStep = true;
                    LogProcessSummary(currentNumberedStep);
                    return;
                }

                _processSummary.StepDescription = currentNumberedStep.Step.NavigationInstruction.Instructions.Replace("\n", " ");
                CheckIfMultiSegment(currentNumberedStep.Step, texpressSegments);

                var timeChoices = GetOrderedTEXpressPriceLookupKeys(routeSteps, currentNumberedStep.StepNumber);

                var (startSegment, startEntryPoint) = GetStepStartSegment(texpressSegments.Where(s => cardinalDirections.Contains(s.CardinalDirection)), texpressStep.StartLocation);

                // Debug analysis
                var nearestStartSegment = texpressSegments.MinBy(segment => segment.EntryPoints.Min(entryPoint => entryPoint.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate())));
                var nearestEntry = nearestStartSegment?.EntryPoints.MinBy(entryPoint => entryPoint.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()));

                if (startSegment is null)
                {
                    _logger.LogWarning("Could not find start segment: Step Count {StepCount} | Description \"{StepDescription}\" | Start LatLng {StartLatLng}",
                        currentNumberedStep.StepNumber,
                        texpressStep.NavigationInstruction.Instructions.Replace("\n", " "),
                        JsonSerializer.Serialize(texpressStep.StartLocation.LatLng));

                    _processSummary.StartAccessInfo = new()
                    {
                        Matched = false,
                        RouteCoordinateToMatch = texpressStep.StartLocation.ToCoordinate(),
                        MatchedSegmentName = null,
                        MatchedAccessPoint = null,
                        MatchedDistanceToRouteCoordinate = null,
                        SegmentPriceAdded = null,

                        ActualClosestSegmentName = nearestStartSegment?.Description,
                        ActualClosestAccessPoint = nearestEntry?.Description,
                        ActualClosestCardinalDirection = nearestStartSegment?.CardinalDirection.ToString(),
                        ActualClosestCoordinate = nearestEntry?.Location,
                        ActualClosestDistanceToRouteCoordinate = nearestEntry?.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()),
                    };

                    matchedAllSegments = false;
                }
                else
                {
                    startSegment?.EntryPoints.ToList().ForEach(ep =>
                    {
                        if (ep.SkipWaypoint is not null) skipWaypoints.Add((Coordinate)ep.SkipWaypoint);
                    });
                    _logger.LogInformation("Matched start segment: {SegmentDescription} | {EntryPoint}", startSegment.Description, startEntryPoint?.Description);
                    double price = GetTollSegmentPrice(timeChoices, startSegment);
                    _logger.LogInformation("Adding price {Price} for this step", price);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;

                    _processSummary.StartAccessInfo = new()
                    {
                        Matched = true,
                        RouteCoordinateToMatch = texpressStep.StartLocation.ToCoordinate(),
                        MatchedSegmentName = startSegment.Description,
                        MatchedAccessPoint = startEntryPoint?.Description,
                        MatchedDistanceToRouteCoordinate = startEntryPoint?.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()),
                        SegmentPriceAdded = price,

                        ActualClosestSegmentName = nearestStartSegment?.Description,
                        ActualClosestAccessPoint = nearestEntry?.Description,
                        ActualClosestCardinalDirection = nearestStartSegment?.CardinalDirection.ToString(),
                        ActualClosestCoordinate = nearestEntry?.Location,
                        ActualClosestDistanceToRouteCoordinate = nearestEntry?.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()),
                    };
                }

                if (EndsInSameSegment(texpressStep.EndLocation, startSegment))
                {
                    _logger.LogInformation("Step ends in same segment as start segment, skipping end segment price check.");
                    _processSummary.EndsInSameSegment = true;
                    var exitPoint = startSegment?.ExitPoints.First(e => e.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()) <= _tollAccessPointMatchToleranceMiles);

                    _processSummary.EndAccessInfo = new()
                    {
                        Matched = true,
                        MatchedSegmentName = startSegment?.Description,
                        MatchedAccessPoint = exitPoint?.Description,
                        MatchedDistanceToRouteCoordinate = exitPoint?.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()),
                        RouteCoordinateToMatch = texpressStep.EndLocation.ToCoordinate(),
                    };
                    return;
                }

                var (endSegment, endExitPoint) = GetStepEndSegment(texpressSegments, texpressStep.EndLocation);
                // Debug analysis
                var nearestExitSegment = texpressSegments.MinBy(segment => segment.ExitPoints.Min(exitPoint => exitPoint.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate())));
                var nearestExitPoint = nearestExitSegment?.ExitPoints.MinBy(exitPoint => exitPoint.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()));

                if (endSegment is null)
                {
                    _logger.LogWarning("Could not find end segment: Step Count {StepCount} | Description \"{StepDescription}\" | End LatLng {EndLatLng}",
                        currentNumberedStep.StepNumber,
                        texpressStep.NavigationInstruction.Instructions,
                        JsonSerializer.Serialize(texpressStep.EndLocation.LatLng));

                    _processSummary.EndAccessInfo = new()
                    {
                        Matched = false,
                        RouteCoordinateToMatch = texpressStep.EndLocation.ToCoordinate(),
                        MatchedSegmentName = null,
                        MatchedAccessPoint = null,
                        MatchedDistanceToRouteCoordinate = null,
                        SegmentPriceAdded = null,

                        ActualClosestSegmentName = nearestExitSegment?.Description,
                        ActualClosestAccessPoint = nearestExitPoint?.Description,
                        ActualClosestCardinalDirection = nearestExitSegment?.CardinalDirection.ToString(),
                        ActualClosestCoordinate = nearestExitPoint?.Location,
                        ActualClosestDistanceToRouteCoordinate = nearestExitPoint?.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()),
                    };
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched end segment: {SegmentDescription}", endSegment.Description);
                    var price = GetTollSegmentPrice(timeChoices, endSegment);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;

                    _processSummary.EndAccessInfo = new()
                    {
                        Matched = true,
                        RouteCoordinateToMatch = texpressStep.EndLocation.ToCoordinate(),
                        MatchedSegmentName = endSegment.Description,
                        MatchedAccessPoint = endExitPoint?.Description,
                        MatchedDistanceToRouteCoordinate = endExitPoint?.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()),
                        SegmentPriceAdded = price,

                        ActualClosestSegmentName = nearestExitSegment?.Description,
                        ActualClosestAccessPoint = nearestExitPoint?.Description,
                        ActualClosestCardinalDirection = nearestExitSegment?.CardinalDirection.ToString(),
                        ActualClosestCoordinate = nearestExitPoint?.Location,
                        ActualClosestDistanceToRouteCoordinate = nearestExitPoint?.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()),
                    };
                }

                LogProcessSummary(currentNumberedStep);
            });

            var tollResponse = new TEXpressTollPriceResult
            {
                TotalTollPrice = hasTollTag ? totalTollPrice : (totalTollPrice * (_noTollTagPriceMultiplier)),
                MatchedAllSegments = matchedAllSegments,
                HasTollSteps = true,
                SkipWaypoints = skipWaypoints,
            };

            _logger.LogInformation("Completed TEXpress toll price calculation. Total Price: {TotalPrice} | Matched All Segments: {MatchedAllSegments} | Has Toll Steps: {HasTollSteps}",
                tollResponse.TotalTollPrice,
                tollResponse.MatchedAllSegments,
                tollResponse.HasTollSteps);
            return tollResponse;
        }

        private void LogProcessSummary(NumberedRouteStep currentNumberedStep)
        {
            _logger.LogDebug("\n\n\nProcess Summary for Step Number {StepNumber}:\n{ProcessSummary}",
                                currentNumberedStep.StepNumber,
                                JsonSerializer.Serialize(_processSummary, new JsonSerializerOptions
                                {
                                    WriteIndented = true,
                                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                                }));
        }

        private void CheckIfMultiSegment(RouteLegStep step, IEnumerable<TEXpressSegment> texpressSegments)
        {
            var polyline = step.Polyline.EncodedPolyline;
            var coords = PolylineEncoder_TEMP.Decode(polyline).ToList();
            var dictionary = new Dictionary<string, int>();

            var cardinalDirections = coords.First().GetCardinalDirection(coords.Last());

            texpressSegments.Where(s => cardinalDirections.Contains(s.CardinalDirection)).ToList().ForEach(segment =>
            {
                var entryPoints = segment.EntryPoints.Select(ep => ep.Location);
                int matchCount = coords.Count(coord => entryPoints.Any(point => point.DistanceToInMiles(coord) <= _tollAccessPointMatchToleranceMiles));
                if (matchCount > 0)
                {
                    dictionary.Add(segment.Description ?? "UNKNOWN", matchCount);
                }
            });

            if (dictionary.Count == 0)
            {
                _processSummary.MatchedSegmentsDescription = "No matching segments found for this step polyline.";
            }
            else
            {
                _processSummary.MatchedSegmentsDescription = $"Matched {dictionary.Count} segment(s): {string.Join(", ", dictionary.Keys)}";
            }
        }

        public async Task PrintPoints(Coordinate pointToMatch, string tollSegmentName, bool entryPoints = true)
        {
            var tollSegments = await GetSegmentsAsync();
            var segmentToAnalyze = tollSegments.First(s => string.Equals(s.Description, tollSegmentName, StringComparison.OrdinalIgnoreCase));

            var accessPoints = entryPoints ? segmentToAnalyze.EntryPoints : segmentToAnalyze.ExitPoints;

            foreach (var accessPoint in accessPoints)
            {
                var distance = accessPoint.Location.DistanceToInMiles(pointToMatch);
                _logger.LogInformation($"Access Point: {accessPoint.Description} | LatLng: {accessPoint.Location.Latitude}, {accessPoint.Location.Longitude} | Distance to Match: {distance} miles");
            }
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

        private bool IsTakeRampStep(IEnumerable<NumberedRouteStep> tollSteps, NumberedRouteStep currentStep)
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
        public List<Coordinate> SkipWaypoints { get; set; } = new();
    }

    public class NumberedRouteStep
    {
        public int StepNumber { get; set; }
        public required RouteLegStep Step { get; set; }
    }

    public class DebugLogProcessSummary
    {
        public string? StepDescription { get; set; }
        public string? StepPolyline { get; set; }
        public string? StepManeuver { get; set; }
        public string? StepCardinalDirection { get; set; }
        public bool? SkippedDueToRampStep { get; set; } = false;
        public string? MatchedSegmentsDescription { get; set; }
        public DebugLogAccessPointSummary? StartAccessInfo { get; set; }
        public bool? EndsInSameSegment { get; set; }
        public DebugLogAccessPointSummary? EndAccessInfo { get; set; }
    }

    public class DebugLogAccessPointSummary
    {
        public bool? Matched { get; set; }
        public Coordinate? RouteCoordinateToMatch { get; set; }
        public string? RouteCoordinateToMatchFormatted => RouteCoordinateToMatch is not null ? $"{RouteCoordinateToMatch.Value.Latitude}, {RouteCoordinateToMatch.Value.Longitude}" : null;
        public string? MatchedSegmentName { get; set; }
        public string? MatchedAccessPoint { get; set; }
        public double? MatchedDistanceToRouteCoordinate { get; set; }
        public double? SegmentPriceAdded { get; set; }

        public string? ActualClosestSegmentName { get; set; }
        public string? ActualClosestAccessPoint { get; set; }
        public string? ActualClosestCardinalDirection { get; set; }
        public Coordinate? ActualClosestCoordinate { get; set; }
        public double? ActualClosestDistanceToRouteCoordinate { get; set; }
    }
}
