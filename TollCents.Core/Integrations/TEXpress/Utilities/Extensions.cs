using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using System.Globalization;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.TEXpress.Entities;

namespace TollCents.Core.Integrations.TEXpress.Utilities
{
    internal static class Extensions
    {
        internal static Coordinate ToCoordinate(this RouteLocation location)
        {
            return new Coordinate
            {
                Latitude = location.LatLng.Latitude,
                Longitude = location.LatLng.Longitude
            };
        }

        internal static TEXpressPriceLookupChoice ToPriceLookupChoice(this DateTime dateTime)
        {
            return new TEXpressPriceLookupChoice
            {
                ChoiceDay = dateTime.ToString("dddd"),
                ChoiceTime = dateTime.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            };
        }

        internal static IEnumerable<TEXpressSegment> MatchesCardinalDirections(this IEnumerable<TEXpressSegment> segments, IEnumerable<CardinalDirection>? directions)
        {
            if (directions is null || !directions.Any())
            {
                return Enumerable.Empty<TEXpressSegment>();
            }

            return segments.Where(segment => segment.CardinalDirections.Intersect(directions).Any());
        }

        internal static IEnumerable<CardinalDirection> GetCardinalDirections(this RouteLegStep routeStep)
        {
            return routeStep.StartLocation.ToCoordinate().GetCardinalDirections(routeStep.EndLocation.ToCoordinate());
        }
    }
}
