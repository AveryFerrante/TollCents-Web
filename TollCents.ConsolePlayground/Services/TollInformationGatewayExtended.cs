using GoogleApi.Entities.Maps.Routes.Directions.Response;
using GoogleApi.Interfaces.Maps.Routes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;
using TollCents.Core.Integrations.GoogleMaps.Utilities;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.ConsolePlayground.Services;

public interface ITollInformationGatewayExtended : ITollInformationGateway
{
    Task<RoutesDirectionsResponse> GetTollRouteDataRawAsync(RouteRequestBase routeRequest);

    Task<RoutesDirectionsResponse> GetNonTollRouteDataRawAsync(RouteRequestBase addressRequest);
}

public class TollInformationGatewayExtended : TollInformationGateway, ITollInformationGatewayExtended
{
    private readonly string _apiKey;
    private readonly IRoutesDirectionsApi _routesDirectionsApi;

    public TollInformationGatewayExtended(
        IRoutesDirectionsApi routesDirectionsApi,
        IOptions<GoogleMapsIntegrationConfiguration> configuration,
        ITEXpressTollPriceCalculator texpressTollPriceCalculator,
        ILogger<TollInformationGateway> logger) : base(routesDirectionsApi, configuration, texpressTollPriceCalculator, logger)
    {
        _apiKey = configuration.Value.ApiKey;
        _routesDirectionsApi = routesDirectionsApi;
    }

    public async Task<RoutesDirectionsResponse> GetTollRouteDataRawAsync(RouteRequestBase routeRequest)
    {
        var request = RouteRequestBuilder
            .GetRequest(routeRequest, _apiKey)
            .IncludeTolls(routeRequest.IncludeTollPass ?? false ? new List<string> { "US_TX_TOLLTAG" } : null, null);

        return await _routesDirectionsApi.QueryAsync(request);
    }

    public async Task<RoutesDirectionsResponse> GetNonTollRouteDataRawAsync(RouteRequestBase addressRequest)
    {
        var request = RouteRequestBuilder
            .GetRequest(addressRequest, _apiKey)
            .AvoidTolls();

        return await _routesDirectionsApi.QueryAsync(request);
    }
}
