using GoogleApi.Entities.Maps.Routes.Common;

namespace TollCents.Core.Integrations.GoogleMaps.Requests
{
    public class ByAddressRequest : RouteRequestBase
    {
        public required string StartAddress { get; set; }

        public required string EndAddress { get; set; }

        public override RouteWayPoint GetRouteDestination()
        {
            return new RouteWayPoint { Address = EndAddress };
        }

        public override RouteWayPoint GetRouteOrigin()
        {
            return new RouteWayPoint { Address = StartAddress };
        }
    }
}
