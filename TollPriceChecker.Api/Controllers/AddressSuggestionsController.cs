using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TollPriceChecker.Core.Entities;
using TollPriceChecker.Core.Integrations.GoogleMaps;

namespace TollPriceChecker.Api.Controllers
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
