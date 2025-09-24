namespace TollCents.Core.Entities
{
    public class TollRouteInformation : RouteInformation
    {
        public double TollPrice { get; set; }
        public bool HasDynamicTolls { get; set; }
    }
}
