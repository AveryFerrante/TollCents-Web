using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Interfaces.Maps.Routes;
using Microsoft.Extensions.Logging;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress;

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
        private readonly ITEXpressTollPriceCalculator _texpressTollPriceCalculator;
        private readonly ILogger<TollInformationGateway> _logger;
        private readonly string _apiKey;

        public TollInformationGateway(IRoutesDirectionsApi routesDirectionsApi, IIntegrationsConfiguration configuration,
            ITEXpressTollPriceCalculator texpressTollPriceCalculator, ILogger<TollInformationGateway> logger)
        {
            var apiKey = configuration?.Integrations?.GoogleMaps?.ApiKey;
            ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(configuration.Integrations.GoogleMaps.ApiKey));
            _routesDirectionsApi = routesDirectionsApi;
            _texpressTollPriceCalculator = texpressTollPriceCalculator;
            _apiKey = apiKey;
            _logger = logger;
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);

            var response = await _routesDirectionsApi.QueryAsync(request);

            return await MapToTollRouteInformation(response, addressRequest.IncludeTollPass ?? false);
        }

        public async Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .IncludeTolls(addressRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);

            var response = await _routesDirectionsApi.QueryAsync(request);
            _logger.LogInformation("Processesing results for route from {StartAddress} to {EndAddress}",
                addressRequest.StartAddress, addressRequest.EndAddress);

            return await MapToTollRouteInformation(response, addressRequest.IncludeTollPass ?? false);
        }

        public async Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest)
        {
            var request = RouteBaseRequest
                .GetRequest(addressRequest, _apiKey)
                .AvoidTolls();

            var response = await _routesDirectionsApi.QueryAsync(request);

            return MapToRouteInformation(response);
        }
        private async Task<TollRouteInformation?> MapToTollRouteInformation(RoutesDirectionsResponse response, bool hasTollPass)
        {
            if (response is null || response.Status != Status.Ok || !response.Routes.Any())
                return null;

            var route = response.Routes.First();
            var routeLeg = route.Legs?.FirstOrDefault();
            var distanceInMiles = route.DistanceMeters * 0.000621371 ?? 0;
            var tollPriceUnits = Convert.ToInt32(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Units ?? "0");
            var tollPriceNanos = Convert.ToDouble(route.TravelAdvisory?.TollInfo?.EstimatedPrice?.FirstOrDefault()?.Nanos ?? 0) / 1000000000;
            var texpressTolls = await _texpressTollPriceCalculator.GetTEXpressTollPrice(
                routeLeg?.Steps ?? Enumerable.Empty<RouteLegStep>(),
                hasTollPass);
            return new TollRouteInformation
            {
                DistanceInMiles = distanceInMiles,
                DriveTime = new DriveTime
                {
                    Hours = route.Duration?.Hours ?? 0,
                    Minutes = route.Duration?.Minutes ?? 0
                },
                GuaranteedTollPrice = tollPriceUnits + tollPriceNanos,
                EstimatedDynamicTollPrice = texpressTolls.TotalTollPrice,
                Description = route.Description,
                HasDynamicTolls = texpressTolls.HasTollSteps,
                ProcessedAllDynamicTolls = texpressTolls.MatchedAllSegments
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

    public class TollInformationGatewayMock : ITollInformationGateway
    {
        private readonly RouteInformation? _mockRouteInformation = new()
        {
            DistanceInMiles = 14.3,
            DriveTime = new()
            {
                Hours = 0,
                Minutes = 32
            },
            Description = "One road to some other road"
        };

        private readonly TollRouteInformation? _mockTollRouteInformation = new()
        {
            DistanceInMiles = 15.2,
            Description = "One toll road to some other road",
            DriveTime = new()
            {
                Hours = 0,
                Minutes = 22
            },
            EstimatedDynamicTollPrice = 3.24,
            HasDynamicTolls = true,
            ProcessedAllDynamicTolls = false,
            GuaranteedTollPrice = 2.00
        };

        public Task<RouteInformation?> GetRouteAvoidTollInformationAsync(ByAddressRequest addressRequest) => Task.FromResult(_mockRouteInformation);

        public Task<TollRouteInformation?> GetRouteTollInformationAsync(ByAddressRequest addressRequest) => Task.FromResult(_mockTollRouteInformation);

        public Task<TollRouteInformation?> GetRouteTollInformationTXAsync(ByAddressRequest addressRequest) => Task.FromResult(_mockTollRouteInformation);
    }

}
