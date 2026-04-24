using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
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

        public async Task<bool> TestTEXpress(IEnumerable<RouteLegStep> routeSteps)
        {
            var numberedTEXpressSteps = routeSteps
                .Select((step, index) => new NumberedRouteStep { Step = step, StepNumber = index })
                .Where(a => IsTEXpressTollStep(a.Step)).ToList();

            if (!numberedTEXpressSteps.Any())
            {
                _logger.LogInformation("No TEXpress steps found in route.");
                return false;
            }

            var texpressSegments = await GetSegmentsAsync();
            return false;
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
                var isTexpressStep = IsTEXpressTollStep(s);
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
            var numberedTEXpressSteps = routeSteps
                .Select((step, index) => new NumberedRouteStep { Step = step, StepNumber = index })
                .Where(a => IsTEXpressTollStep(a.Step)).ToList();
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
                _logger.LogInformation("Analyzing TEXpress Step Number {StepNumber} | Description \"{StepDescription}\"",
                    currentNumberedStep.StepNumber,
                    currentNumberedStep.Step.NavigationInstruction.Instructions.Replace("\n", " "));
                if (IsTakeRampStep(numberedTEXpressSteps, currentNumberedStep))
                {
                    return;
                }

                CheckIfMultiSegment(currentNumberedStep.Step, texpressSegments);

                var timeChoices = GetOrderedTEXpressPriceLookupKeys(routeSteps, currentNumberedStep.StepNumber);
                var texpressStep = currentNumberedStep.Step;
                
                var cardinalDirection = texpressStep.StartLocation.ToCoordinate().GetCardinalDirection(texpressStep.EndLocation.ToCoordinate());
                _logger.LogInformation("Cardinal direction of step polyline: {CardinalDirection}", cardinalDirection.ToString());

                var (startSegment, startEntryPoint) = GetStepStartSegment(texpressSegments.Where(s => s.CardinalDirection == cardinalDirection), texpressStep.StartLocation);

                if (startSegment is null)
                {
                    _logger.LogWarning("Could not find start segment: Step Count {StepCount} | Description \"{StepDescription}\" | Start LatLng {StartLatLng}",
                        currentNumberedStep.StepNumber,
                        texpressStep.NavigationInstruction.Instructions.Replace("\n", " "),
                        JsonSerializer.Serialize(texpressStep.StartLocation.LatLng));

                    var nearestSegment = texpressSegments.MinBy(segment => segment.EntryPoints.Min(entryPoint => entryPoint.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate())));
                    var nearestEntry = nearestSegment?.EntryPoints.MinBy(entryPoint => entryPoint.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()));
                    _logger.LogWarning("Nearest entry point segment {Segment} at entry point {Entrypoint} with distance {Distance}",
                        nearestSegment!.Description,
                        nearestEntry!.Description,
                        nearestEntry.Location.DistanceToInMiles(texpressStep.StartLocation.ToCoordinate()));

                    _logger.LogInformation("Associated polyline for this step: {Polyline}", texpressStep.Polyline.EncodedPolyline);
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

                    var nearestSegment = texpressSegments.MinBy(segment => segment.ExitPoints.Min(exitPoint => exitPoint.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate())));
                    var nearestEntry = nearestSegment?.EntryPoints.MinBy(entryPoint => entryPoint.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()));
                    _logger.LogWarning("Nearest exit point segment {Segment} at exit point {ExitPoint} with distance {Distance}",
                        nearestSegment!.Description,
                        nearestEntry!.Description,
                        nearestEntry.Location.DistanceToInMiles(texpressStep.EndLocation.ToCoordinate()));
                    matchedAllSegments = false;
                }
                else
                {
                    _logger.LogInformation("Matched end segment: {SegmentDescription}", endSegment.Description);
                    var price = GetTollSegmentPrice(timeChoices, endSegment);
                    _logger.LogInformation("Adding price {Price} for this step", price);
                    if (price > 0) totalTollPrice += price;
                    else matchedAllSegments = false;
                }
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

        private void CheckIfMultiSegment(RouteLegStep step, IEnumerable<TEXpressSegment> texpressSegments)
        {
            var polyline = step.Polyline.EncodedPolyline;
            var coords = PolylineEncoder_TEMP.Decode(polyline).ToList();
            var dictionary = new Dictionary<string, int>();

            var cardinalDirection = coords.First().GetCardinalDirection(coords.Last());

            texpressSegments.Where(s => s.CardinalDirection == cardinalDirection).ToList().ForEach(segment =>
            {
                var entryPoints = segment.EntryPoints.Select(ep => ep.Location);
                int matchCount = coords.Count(coord => entryPoints.Any(point => point.DistanceToInMiles(coord) <= _tollAccessPointMatchToleranceMiles));
                if (matchCount > 0)
                {
                    dictionary.Add(segment.Description ?? "UNKNOWN", matchCount);
                }
            });

            foreach (KeyValuePair<string, int> pair in dictionary)
            {
                _logger.LogInformation("Segment {SegmentDescription} has {MatchCount} matching coordinates in step polyline.", pair.Key, pair.Value);
            }

            if (dictionary.Keys.Count > 1)
            {
                _logger.LogInformation("This is likely a multistep segment!");
            }
            else
            {
                _logger.LogInformation("This is likely a single segment step.");
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

        private (TEXpressSegment?, TollAccessPoint?) GetStepStartSegment(IEnumerable<TEXpressSegment> texpressSegments, RouteLocation stepLocation)
        {
            var stepStartLocation = stepLocation.ToCoordinate();
            var matchedSegment = texpressSegments
                .FirstOrDefault(segment => segment.EntryPoints
                    .Any(entryPoint => entryPoint.Location.DistanceToInMiles(stepStartLocation) <= _tollAccessPointMatchToleranceMiles));
            var entryPoint = matchedSegment?.EntryPoints.MinBy(point => point.Location.DistanceToInMiles(stepStartLocation));

            return (matchedSegment, entryPoint);
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
        public List<Coordinate> SkipWaypoints { get; set; } = new();
    }

    public class NumberedRouteStep
    {
        public int StepNumber { get; set; }
        public required RouteLegStep Step { get; set; }
    }
}
