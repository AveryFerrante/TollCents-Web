using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TollCents.Api.Startup;
using TollCents.Core.Entities;
using TollCents.Core.Integrations.GoogleMaps;

namespace TollCents.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/address-suggestions")]
    public class AddressSuggestionsController : ControllerBase
    {
        private readonly IAddressLookupGateway _addressLookupGateway;
        private readonly IApiRuntimeConfiguration _apiRuntimeConfiguration;
        private IEnumerable<PlaceSuggestion> _mockedSuggestions = new List<PlaceSuggestion>()
        {
            new PlaceSuggestion
            {
                Name = "145 Mock Address Road, Fake City, TX"
            },
            new PlaceSuggestion
            {
                Name = "187 Cool Street, Faker City, OK"
            }
        };

        public AddressSuggestionsController(IAddressLookupGateway addressLookupGateway,
            IApiRuntimeConfiguration apiRuntimeConfiguration)
        {
            _addressLookupGateway = addressLookupGateway;
            _apiRuntimeConfiguration = apiRuntimeConfiguration;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaceSuggestion>>> GetPlaceSuggestionsAsync([FromQuery] string queryHint)
        {
            if (_apiRuntimeConfiguration.MockAPIs)
            {
                return Ok(_mockedSuggestions);
            }
            var results = await _addressLookupGateway.GetPlaceSuggestionsAsync(queryHint);
            return Ok(results);
        }

        [HttpGet("geolocation")]
        public async Task<ActionResult<PlaceSuggestion>> GetPlaceSuggestionByLatLong([FromQuery] double latitude, [FromQuery] double longitude)
        {
            if (_apiRuntimeConfiguration.MockAPIs)
            {
                return Ok(_mockedSuggestions.First());
            }
            var results = await _addressLookupGateway.GetPlaceSuggestionAsync(latitude, longitude);
            if (results is null)
                return NoContent();
            return Ok(results);
        }
    }
}
