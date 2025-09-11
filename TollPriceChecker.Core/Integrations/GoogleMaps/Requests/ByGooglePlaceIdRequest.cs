namespace TollPriceChecker.Core.Integrations.GoogleMaps.Requests
{
    public class ByGooglePlaceIdRequest
    {
        public required string StartPlaceId { get; set; }
        public required string EndPlaceId { get; set; }
        public bool? IncludeTollPass { get; set; } = false;
    }
}
