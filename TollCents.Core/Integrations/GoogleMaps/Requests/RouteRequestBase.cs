using GoogleApi.Entities.Maps.Routes.Common;
using TollCents.Core.Entities;

namespace TollCents.Core.Integrations.GoogleMaps.Requests
{
    public abstract class RouteRequestBase
    {
        /// <summary>
        /// Optional list of coordinates representing waypoints a route must pass through.
        /// </summary>
        public IEnumerable<Coordinate>? ViaWaypoints { get; set; }

        public bool? IncludeTollPass { get; set; } = false;

        public abstract RouteWayPoint GetRouteOrigin();

        public abstract RouteWayPoint GetRouteDestination();
    }
}
