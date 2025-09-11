namespace TollPriceChecker.Core.Entities
{
    public class RouteInformation
    {
        public double DistanceInMiles { get; set; }
        public string? Description { get; set; }
        public required DriveTime DriveTime { get; set; }
    }

    public class DriveTime
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
    }
}
