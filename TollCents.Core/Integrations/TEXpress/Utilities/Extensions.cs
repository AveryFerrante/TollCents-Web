using GoogleApi.Entities.Maps.Routes.Common;
using System.Globalization;
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
    }
}
