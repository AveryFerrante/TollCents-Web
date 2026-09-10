using GoogleApi.Entities.Common;
using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Maps.Routes.Common;
using GoogleApi.Entities.Maps.Routes.Common.Enums;
using GoogleApi.Entities.Maps.Routes.Directions.Request;
using GoogleApi.Entities.Maps.Routes.Directions.Response.Enums;
using TollCents.Core.Integrations.GoogleMaps.Requests;

namespace TollCents.Core.Integrations.GoogleMaps.Utilities
{
    public interface IRouteRequestBuilder
    {
        RoutesDirectionsRequest IncludeTolls(IEnumerable<string>? tollPasses, VehicleEmissionType? vehicleEmissionType);
        RoutesDirectionsRequest AvoidTolls();
    }

    public class RouteRequestBuilder : IRouteRequestBuilder
    {
        private RoutesDirectionsRequest _request;
        private const string _fieldMaskCommon = "routes.duration,routes.distanceMeters,routes.description";
        private const string _fieldMaskTollInfo = "routes.travelAdvisory.tollInfo,routes.legs.steps";
        private const string _tempTestInfo = "routes.polyline";
        private RouteRequestBuilder(RoutesDirectionsRequest request)
        {
            _request = request;
        }
        public static IRouteRequestBuilder GetRequest(RouteRequestBase routeRequest, string apiKey)
        {
            var request = new RoutesDirectionsRequest
            {
                Key = apiKey,
                Origin = routeRequest.GetRouteOrigin(),
                Destination = routeRequest.GetRouteDestination(),
                Intermediates = routeRequest.ViaWaypoints?.Select(wp => new RouteWayPoint { Location = new RouteLocation { LatLng = new LatLng(wp.Latitude, wp.Longitude) }, Via = true }).ToList() ?? [],
                Region = "US",
                Language = Language.English,
                RoutingPreference = RoutingPreference.TrafficAwareOptimal,
            };
            return new RouteRequestBuilder(request);
        }

        public RoutesDirectionsRequest AvoidTolls()
        {
            _request.RouteModifiers = new RouteModifiers
            {
                AvoidTolls = true,
            };
            _request.FieldMask = $"{_fieldMaskCommon},{_tempTestInfo}";
            return _request;
        }

        public RoutesDirectionsRequest IncludeTolls(IEnumerable<string>? tollPasses, VehicleEmissionType? vehicleEmissionType)
        {
            if (tollPasses is null)
                tollPasses = new List<string>();

            _request.ExtraComputations = new List<ExtraComputation> { ExtraComputation.Tolls };
            _request.RouteModifiers = new RouteModifiers
            {
                AvoidTolls = false,
                TollPasses = tollPasses,
                VehicleInfo = new VehicleInfo
                {
                    EmissionType = vehicleEmissionType ?? VehicleEmissionType.Gasoline,
                }
            };
            _request.FieldMask = string.Join(",", [_fieldMaskCommon, _fieldMaskTollInfo, _tempTestInfo]);
            return _request;
        }
    }
}
