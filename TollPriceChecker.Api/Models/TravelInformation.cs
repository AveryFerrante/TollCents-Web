using TollPriceChecker.Core.Entities;

namespace TollPriceChecker.Api.Models
{
    public class TravelInformation
    {
        public required RouteInformation AvoidTollsRouteInformation { get; set; }
        public TollRouteInformation? TollRouteInformation { get; set; }
    }
}
