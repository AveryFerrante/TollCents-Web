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
    public class AddressSuggestionsController(IAddressLookupGateway addressLookupGateway) : ControllerBase
    {
        private readonly IAddressLookupGateway _addressLookupGateway = addressLookupGateway;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaceSuggestion>>> GetPlaceSuggestionsAsync([FromQuery] string queryHint)
        {
            var results = await _addressLookupGateway.GetPlaceSuggestionsAsync(queryHint);
            return Ok(results);
        }

        [HttpGet("geolocation")]
        public async Task<ActionResult<PlaceSuggestion>> GetPlaceSuggestionByLatLong([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var results = await _addressLookupGateway.GetPlaceSuggestionAsync(latitude, longitude);
            if (results is null)
                return NoContent();
            return Ok(results);
        }
    }
}
