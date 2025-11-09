using GoogleApi.Entities.Common;
using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Maps.Geocoding.Location.Request;
using GoogleApi.Entities.Places.AutoComplete.Request;
using GoogleApi.Interfaces.Maps.Geocode;
using GoogleApi.Interfaces.Places;
using TollCents.Core.Entities;

namespace TollCents.Core.Integrations.GoogleMaps
{
    public interface IAddressLookupGateway
    {
        Task<IEnumerable<PlaceSuggestion>> GetPlaceSuggestionsAsync(string address);
        Task<PlaceSuggestion?> GetPlaceSuggestionAsync(double latitude, double longitude);

    }

    public class AddressLookupGateway : IAddressLookupGateway
    {
        private readonly IAutoCompleteApi _autoCompleteApi;
        private readonly ILocationGeocodeApi _locationGeocodeApi;
        private readonly string _apiKey;

        public AddressLookupGateway(IAutoCompleteApi autoCompleteApi,
            ILocationGeocodeApi locationGeocodeApi, IIntegrationsConfiguration configuration)
        {
            var apiKey = configuration?.Integrations?.GoogleMaps?.ApiKey;
            ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(configuration.Integrations.GoogleMaps.ApiKey));
            _autoCompleteApi = autoCompleteApi;
            _locationGeocodeApi = locationGeocodeApi;
            _apiKey = apiKey;
        }

        public async Task<IEnumerable<PlaceSuggestion>> GetPlaceSuggestionsAsync(string address)
        {
            var request = new PlacesAutoCompleteRequest
            {
                Key = _apiKey,
                Input = address,
            };
            var response = await _autoCompleteApi.QueryAsync(request);
            if (response is null || response.Status != Status.Ok || !(response.Predictions?.Any() ?? false))
            {
                return Enumerable.Empty<PlaceSuggestion>();
            }
            return response.Predictions.Where(p => p.Terms.Count() >= 4).Select(p => new PlaceSuggestion
            {
                Name = p.Description
            });
        }

        public async Task<PlaceSuggestion?> GetPlaceSuggestionAsync(double latitude, double longitude)
        {
            var request = new LocationGeocodeRequest
            {
                Key = _apiKey,
                Location = new Coordinate(latitude, longitude),
                ResultTypes = new List<LocationResultType>
                {
                    LocationResultType.Street_Address,
                    LocationResultType.Intersection
                }
            };
            var response = await _locationGeocodeApi.QueryAsync(request);
            if (response is null || response.Status != Status.Ok || !(response.Results?.Any() ?? false))
            {
                return null;
            }
            var addressResult = response.Results.First();
            return new PlaceSuggestion
            {
                Name = addressResult.FormattedAddress
            };
        }
    }
}
