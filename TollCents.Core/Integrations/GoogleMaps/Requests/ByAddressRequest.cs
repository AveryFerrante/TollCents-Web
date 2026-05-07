using TollCents.Core.Entities;

namespace TollCents.Core.Integrations.GoogleMaps.Requests
{
    public class ByAddressRequest
    {
        public required string StartAddress { get; set; }

        public required string EndAddress { get; set; }

        public bool? IncludeTollPass { get; set; } = false;

        /// <summary>
        /// Optional list of coordinates representing waypoints a route must pass through.
        /// </summary>
        public IEnumerable<Coordinate>? ViaWaypoints { get; set; }
    }
}
