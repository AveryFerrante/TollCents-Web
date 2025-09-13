using TollCents.Core.Entities;

namespace TollCents.Api.Models
{
    public class TravelInformation
    {
        public required RouteInformation AvoidTollsRouteInformation { get; set; }
        public TollRouteInformation? TollRouteInformation { get; set; }
    }
}
