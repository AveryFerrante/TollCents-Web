using GoogleApi.Entities.Common.Enums;
using GoogleApi.Entities.Places.AutoComplete.Request;
using GoogleApi.Interfaces.Places;
using TollPriceChecker.Core.Entities;

namespace TollPriceChecker.Core.Integrations.GoogleMaps
{
    public interface IAddressLookupGateway
    {
        Task<IEnumerable<PlaceSuggestion>> GetPlaceSuggestionsAsync(string address);
    }

    public class AddressLookupGateway : IAddressLookupGateway
    {
        private readonly IAutoCompleteApi _autoCompleteApi;
        private readonly IGoogleMapsIntegrationConfiguration _configuration;

        public AddressLookupGateway(IAutoCompleteApi autoCompleteApi, IGoogleMapsIntegrationConfiguration configuration)
        {
            ArgumentException.ThrowIfNullOrEmpty(configuration.ApiKey, nameof(configuration.ApiKey));
            _autoCompleteApi = autoCompleteApi;
            _configuration = configuration;
        }

        public async Task<IEnumerable<PlaceSuggestion>> GetPlaceSuggestionsAsync(string address)
        {
            var request = new PlacesAutoCompleteRequest
            {
                Key = _configuration.ApiKey!,
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
