using GoogleApi.Entities.Common;
using GoogleApi.Entities.Maps.Routes.Common;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using Coordinate = TollCents.Core.Entities.Coordinate;

namespace TollCents.ConsolePlayground.Services
{
    public class ByCoordinatesRequest : RouteRequestBase
    {
        public required Coordinate Origin { get; set; }

        public required Coordinate Destination { get; set; }

        public override RouteWayPoint GetRouteDestination()
        {
            return new RouteWayPoint
            {
                Location = new RouteLocation
                {
                    LatLng = new LatLng(Destination.Latitude, Destination.Longitude),
                },
            };
        }

        public override RouteWayPoint GetRouteOrigin()
        {
            return new RouteWayPoint
            {
                Location = new RouteLocation
                {
                    LatLng = new LatLng(Origin.Latitude, Origin.Longitude),
                },
            };
        }
    }
}
