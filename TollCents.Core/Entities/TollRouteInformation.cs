namespace TollCents.Core.Entities
{
    public class TollRouteInformation : RouteInformation
    {
        public double GuaranteedTollPrice { get; set; }
        public double EstimatedDynamicTollPrice { get; set; }
        public bool HasDynamicTolls { get; set; }
        public bool ProcessedAllDynamicTolls { get; set; }
    }
}
