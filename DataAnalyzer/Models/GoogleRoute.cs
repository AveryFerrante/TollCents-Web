using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAnalyzer.Models
{
    public class GoogleRoute
    {
        public required IEnumerable<Route> Routes { get; set; }
    }

    public class Route
    {
        public required IEnumerable<RouteLeg> Legs { get; set; }
    }

    public class RouteLeg
    {
        public required IEnumerable<RouteStep> Steps { get; set; }
    }

    public class RouteStep
    {
        public int DistanceMeters { get; set; }
        public required string staticDuration { get; set; }
        public required RoutePolyline Polyline { get; set; }
        public required RouteLocation StartLocation { get; set; }
        public required RouteLocation EndLocation { get; set; }
        public required RouteNavigationInstruction NavigationInstruction { get; set; }

    }

    public class RoutePolyline
    {
        public required string EncodedPolyline { get; set; }
    }

    public class RouteLocation
    {
        public required Coordinate LatLng { get; set; }
    }

    public class Coordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class RouteNavigationInstruction
    {
        public required string Maneuver { get; set; }
        public required string Instructions { get; set; }
    }
}
