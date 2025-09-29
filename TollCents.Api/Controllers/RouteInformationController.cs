using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TollCents.Api.Models;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.GoogleMaps.Requests;

namespace TollCents.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/route-information")]
    public class RouteInformationController : ControllerBase
    {
        private readonly ILogger<RouteInformationController> _logger;
        private readonly ITollInformationGateway _tollInformationGateway;

        public RouteInformationController(ILogger<RouteInformationController> logger, ITollInformationGateway tollInformationGateway)
        {
            _logger = logger;
            _tollInformationGateway = tollInformationGateway;
        }

        [HttpGet]
        public async Task<ActionResult<TravelInformation>> GetTravelInformation(
            [FromQuery] string startAddress,
            [FromQuery] string endAddress,
            [FromQuery] bool hasTollTag)
        {
            var request = new ByAddressRequest { StartAddress = startAddress, EndAddress = endAddress, IncludeTollPass = hasTollTag };
            var tollResponse = await _tollInformationGateway.GetRouteTollInformationTXAsync(request);
            if (tollResponse is null)
                return NoContent();

            if (tollResponse.GuaranteedTollPrice > 0 || tollResponse.HasDynamicTolls)
            {
                var avoidTollResponse = await _tollInformationGateway.GetRouteAvoidTollInformationAsync(request);
                if (avoidTollResponse is null)
                    return NoContent();
                return Ok(new TravelInformation
                {
                    AvoidTollsRouteInformation = avoidTollResponse,
                    TollRouteInformation = tollResponse
                });
            }

            return Ok(new TravelInformation
            {
                AvoidTollsRouteInformation = tollResponse,
                TollRouteInformation = null
            });
        }
    }
}
