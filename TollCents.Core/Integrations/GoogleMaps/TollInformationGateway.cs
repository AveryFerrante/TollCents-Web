using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Interfaces.Maps.Routes;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.GoogleMaps.Utilities;

namespace TollCents.Core.Integrations.GoogleMaps
{
    public interface ITollInformationGateway
    {
        Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest);
        Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest);
        Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest);
    }

    public class TollInformationGateway : ITollInformationGateway
    {
        private readonly IRoutesDirectionsApi _routesDirectionsApi;
        private readonly string _apiKey;

        public TollInformationGateway(IRoutesDirectionsApi routesDirectionsApi, IIntegrationsConfiguration configuration)
        {
            var apiKey = configuration?.Integrations?.GoogleMaps?.ApiKey;
            ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(configuration.Integrations.GoogleMaps.ApiKey));
            _routesDirectionsApi = routesDirectionsApi;
            _apiKey = apiKey;
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);

            var response = await _routesDirectionsApi.QueryAsync(request);

            return MapToTollRouteInformation(response);
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);
            var response = await _routesDirectionsApi.QueryAsync(request);
            return MapToTollRouteInformation(response);
        }

        public async Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .AvoidTolls();

            var response = await _routesDirectionsApi.QueryAsync(request);

            return MapToRouteInformation(response);
        }
        private TollRouteInformation? MapToTollRouteInformation(RoutesDirectionsResponse response)
        {
            if (response is null || response.Status != Status.Ok || !response.Routes.Any())
                return null;

            var route = response.Routes.First();
            var distanceInMiles = route.DistanceMeters * 0.000621371 ?? 0;
            var tollPriceUnits = Convert.ToInt32(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Units ?? "0");
            var tollPriceNanos = Convert.ToDouble(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Nanos ?? 0) / 1000000000;
            return new TollRouteInformation
            {
                DistanceInMiles = distanceInMiles,
                DriveTime = new DriveTime
                {
                    Hours = route.Duration?.Hours ?? 0,
                    Minutes = route.Duration?.Minutes ?? 0
                },
                TollPrice = tollPriceUnits + tollPriceNanos,
                Description = route.Description
            };
        }

        private RouteInformation? MapToRouteInformation(RoutesDirectionsResponse response)
        {
            if (response is null || response.Status != Status.Ok || !response.Routes.Any())
                return null;

            var route = response.Routes.First();
            var distanceInMiles = route.DistanceMeters * 0.000621371 ?? 0;
            return new RouteInformation
            {
                DistanceInMiles = distanceInMiles,
                DriveTime = new DriveTime
                {
                    Hours = route.Duration?.Hours ?? 0,
                    Minutes = route.Duration?.Minutes ?? 0
                },
                Description = route.Description
            };
        }
    }
}
