using TollCents.Core.Integrations.TEXpress.Entities;

namespace TollCents.Core.Integrations.GoogleMaps.Utilities
{
    public static class Extensions
    {
        public static double DistanceToInMiles(this Coordinate coord1, Coordinate coord2)
        {
            const double EarthRadiusMiles = 3958.8;

            double dLat = DegreesToRadians(coord2.Latitude - coord1.Latitude);
            double dLon = DegreesToRadians(coord2.Longitude - coord1.Longitude);

            double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                       Math.Cos(DegreesToRadians(coord1.Latitude)) * Math.Cos(DegreesToRadians(coord2.Latitude)) *
                       Math.Pow(Math.Sin(dLon / 2), 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusMiles * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
