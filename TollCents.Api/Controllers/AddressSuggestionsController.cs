using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public AddressSuggestionsController(IAddressLookupGateway addressLookupGateway)
        {
            _addressLookupGateway = addressLookupGateway;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaceSuggestion>>> GetPlaceSuggestionsAsync([FromQuery] string queryHint)
        {
            var results = await _addressLookupGateway.GetPlaceSuggestionsAsync(queryHint);
            return Ok(results);
        }
    }
}
