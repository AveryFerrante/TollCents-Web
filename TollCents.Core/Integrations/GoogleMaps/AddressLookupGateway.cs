using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Places.AutoComplete.Request;
using GoogleApi.Interfaces.Places;
using TollCents.Core.Entities;

namespace TollCents.Core.Integrations.GoogleMaps
{
    public interface IAddressLookupGateway
    {
        Task<IEnumerable<PlaceSuggestion>> GetPlaceSuggestionsAsync(string address);
    }

    public class AddressLookupGateway : IAddressLookupGateway
    {
        private readonly IAutoCompleteApi _autoCompleteApi;
        private readonly string _apiKey;

        public AddressLookupGateway(IAutoCompleteApi autoCompleteApi, IIntegrationsConfiguration configuration)
        {
            var apiKey = configuration?.Integrations?.GoogleMaps?.ApiKey;
            ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(configuration.Integrations.GoogleMaps.ApiKey));
            _autoCompleteApi = autoCompleteApi;
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
    }
}
