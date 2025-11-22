using DataAnalyzer.Models;
using System.Text.Json;
using TollCents.Core;
using TollCents.Core.Integrations.GoogleMaps.Utilities;

namespace DataAnalyzer
{
    internal class Program
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };  
        static void Main(string[] args)
        {
            string dataDirectory = @"C:\Users\Avery\source\repos\TollCents\DataAnalyzer\Data";
            string withTollsFile = @"with-tolls.txt";
            string withoutTollsFile = @"without-tolls.txt";
            string rawWithTollsData = File.ReadAllText(Path.Combine(dataDirectory, withTollsFile));
            string rawWithoutTollsData = File.ReadAllText(Path.Combine(dataDirectory, withoutTollsFile));
            GoogleRoute? withTollsRoute = JsonSerializer.Deserialize<GoogleRoute>(rawWithTollsData, jsonOptions);
            GoogleRoute? withoutTollsRoute = JsonSerializer.Deserialize<GoogleRoute>(rawWithoutTollsData, jsonOptions);

            ArgumentNullException.ThrowIfNull(withTollsRoute, nameof(withTollsRoute));
            ArgumentNullException.ThrowIfNull(withoutTollsRoute, nameof(withoutTollsRoute));

            IEnumerable<RouteStep> tollRouteSteps = withTollsRoute.Routes.First().Legs.First().Steps;
            IEnumerable<RouteStep> noTollRouteSteps = withoutTollsRoute.Routes.First().Legs.First().Steps;

            //tollRouteSteps.Where(s => s.NavigationInstruction.Instructions.Contains("Toll road", StringComparison.OrdinalIgnoreCase))
            //    .ToList()
            //    .ForEach(s =>
            //    {
            //        // 700 meters ~ 0.43 miles
            //        Console.WriteLine($"Instruction: {s.NavigationInstruction.Instructions} | Distance: {s.DistanceMeters}m " +
            //            $"| Maneuver: {s.NavigationInstruction.Maneuver} | Estimated Entry/Exit: {s.DistanceMeters <= 700}");
            //    });

            int? divergenceStepIndex = GetFirstDivergence(tollRouteSteps, noTollRouteSteps);
            if (divergenceStepIndex == null)
            {
                Console.WriteLine("No divergence found between routes.");
                return;
            }
            int fromStep = divergenceStepIndex.Value + 1;
            int? convergenceStepIndex = GetConvergenceStep(tollRouteSteps.Skip(fromStep), noTollRouteSteps.Skip(fromStep));
            if (convergenceStepIndex == null)
            {
                Console.WriteLine("No convergence found between routes.");
                return;
            }

            var divergenceStep = noTollRouteSteps.ElementAt(divergenceStepIndex.Value);
            var convergenceStep = noTollRouteSteps.ElementAt(fromStep + convergenceStepIndex.Value);
            Console.WriteLine($"Divergence start: {JsonSerializer.Serialize(divergenceStep.StartLocation)} | " +
                $"end: {JsonSerializer.Serialize(divergenceStep.EndLocation)}");

            Console.WriteLine($"Convergence start: {JsonSerializer.Serialize(convergenceStep.StartLocation)} | " +
                $"end: {JsonSerializer.Serialize(convergenceStep.EndLocation)}");
        }

        private static int? GetFirstDivergence(IEnumerable<RouteStep> tollRouteSteps, IEnumerable<RouteStep> noTollRouteSteps)
        {
            int currentIndex = 0;
            bool found = false;
            while (currentIndex < tollRouteSteps.Count() && currentIndex < noTollRouteSteps.Count())
            {
                var tollStep = tollRouteSteps.ElementAt(currentIndex);
                var compareStep = noTollRouteSteps.ElementAt(currentIndex);
                if (tollStep.Polyline.EncodedPolyline != compareStep.Polyline.EncodedPolyline)
                {
                    Console.WriteLine("Divergence found at step index: " + currentIndex);
                    Console.WriteLine("With Tolls Instruction: " + tollStep.NavigationInstruction.Instructions);
                    Console.WriteLine("Without Tolls Instruction: " + compareStep.NavigationInstruction.Instructions);
                    found = true;
                    break;
                }
                currentIndex++;
            }

            return found ? currentIndex : null;
        }

        private static int? GetConvergenceStep(IEnumerable<RouteStep> tollRouteSteps, IEnumerable<RouteStep> noTollRouteSteps)
        {
            int currentIndex = 0;
            bool found = false;
            while (currentIndex < tollRouteSteps.Count() && currentIndex < noTollRouteSteps.Count())
            {
                var tollPoints = PolylineEncoder_TEMP.Decode(tollRouteSteps.ElementAt(currentIndex).Polyline.EncodedPolyline);
                var noTollPoints = PolylineEncoder_TEMP.Decode(noTollRouteSteps.ElementAt(currentIndex).Polyline.EncodedPolyline);

                foreach (var point in tollPoints)
                {
                    if (noTollPoints.Any(ntp => point.DistanceToInMiles(ntp) < 0.05))
                    {
                        Console.WriteLine("Convergence found at step index: " + currentIndex);
                        Console.WriteLine("With Tolls Instruction: " + tollRouteSteps.ElementAt(currentIndex).NavigationInstruction.Instructions);
                        Console.WriteLine("Without Tolls Instruction: " + noTollRouteSteps.ElementAt(currentIndex).NavigationInstruction.Instructions);
                        found = true;
                        break;
                    }
                }
                if (found)
                    break;
                currentIndex++;
            }

            return found ? currentIndex : null;
        }
    }
}
