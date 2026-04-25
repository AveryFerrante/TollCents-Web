using TollCents.Core.Entities;

namespace TollCents.Core
{
    public enum CardinalDirection
    {
        North = 1,
        NorthEast = 2,
        East = 3,
        SouthEast = 4,
        South = 5,
        SouthWest = 6,
        West = 7,
        NorthWest = 8
    }

    public static class Utilities___TEMP
    {
        public static double ToMiles(this double meters)
        {
            return meters * 0.000621371;
        }

        public static double ToMiles(this int meters)
        {
            return Convert.ToDouble(meters).ToMiles();
        }

        private const int EARTH_RADIUS_IN_METERS = 6_378_100;
        public static double GetDistanceToInMeters(this Coordinate start, Coordinate end)
        {
            var startLatitudeRadians = start.Latitude * Math.PI / 180;
            var endLatitudeRadians = end.Latitude * Math.PI / 180;
            var latitudeDeltaRadians = (end.Latitude - start.Latitude) * Math.PI / 180;
            var longitudeDeltaRadians = (end.Longitude - start.Longitude) * Math.PI / 180;

            var haversine = Math.Pow(Math.Sin(latitudeDeltaRadians / 2), 2) +
                (Math.Cos(startLatitudeRadians) * Math.Cos(endLatitudeRadians) * Math.Pow(Math.Sin(longitudeDeltaRadians / 2), 2));

            var angularDistanceRadians = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
            return EARTH_RADIUS_IN_METERS * angularDistanceRadians;
        }

        public static double GetDistanceToInMiles(this Coordinate start, Coordinate end)
        {
            return GetDistanceToInMeters(start, end).ToMiles();
        }

        public static IEnumerable<CardinalDirection> GetCardinalDirection(this Coordinate start, Coordinate end)
        {
            const double percentageOfDistanceRequired = 0.25;
            double distanceRequired = start.GetDistanceToInMiles(end) * percentageOfDistanceRequired;
            var latBearing = start.GetLatitudinalBearing(end, distanceRequired);
            var longBearing = start.GetLongitudinalBearing(end, distanceRequired);
            ThrowIfBothNull(latBearing, longBearing);

            return new[] { latBearing, longBearing }.Where(b => b.HasValue).Select(b => b!.Value);

            //switch (latBearing)
            //{
            //    case null:
            //        return longBearing!.Value;
            //    case CardinalDirection.North:
            //        switch (longBearing)
            //        {
            //            case null:
            //                return latBearing.Value;
            //            case CardinalDirection.East:
            //                return CardinalDirection.NorthEast;
            //            default:
            //                return CardinalDirection.NorthWest;
            //        }
            //    default:
            //        switch (longBearing)
            //        {
            //            case null:
            //                return latBearing.Value;
            //            case CardinalDirection.East:
            //                return CardinalDirection.SouthEast;
            //            default:
            //                return CardinalDirection.SouthWest;
            //        }
            //}
        }

        private static void ThrowIfBothNull(CardinalDirection? latBearing, CardinalDirection? longBearing)
        {
            if (latBearing is null && longBearing is null)
            {
                throw new Exception("Both latitude and longitude bearings were calculated to be null");
            }
        }

        private static CardinalDirection? GetLatitudinalBearing(this Coordinate start, Coordinate end, double distanceRequiredInMiles)
        {
            const double oneDegreeOfLatitudeInMiles = 69.0;
            double requiredLatitudinalVarianceInDegrees = distanceRequiredInMiles / oneDegreeOfLatitudeInMiles;

            var latitudeDelta = start.Latitude - end.Latitude;
            var meetsDistanceRequirement = Math.Abs(latitudeDelta) >= requiredLatitudinalVarianceInDegrees;
            if (latitudeDelta > 0 && meetsDistanceRequirement)
            {
                return CardinalDirection.South;
            }
            if (latitudeDelta < 0 && meetsDistanceRequirement)
            {
                return CardinalDirection.North;
            }
            return null;
        }

        private static CardinalDirection? GetLongitudinalBearing(this Coordinate start, Coordinate end, double distanceRequiredInMiles)
        {
            const double oneDegreeOfLongitudeInMiles = 54.6;
            double requiredLongitudinalVarianceInDegrees = distanceRequiredInMiles / oneDegreeOfLongitudeInMiles;

            var longitudeDelta = start.Longitude - end.Longitude;
            var meetsDistanceRequirement = Math.Abs(longitudeDelta) >= requiredLongitudinalVarianceInDegrees;
            if (longitudeDelta > 0 && meetsDistanceRequirement)
            {
                return CardinalDirection.West;
            }
            if (longitudeDelta < 0 && meetsDistanceRequirement)
            {
                return CardinalDirection.East;
            }
            return null;
        }
    }
}
