using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TollCents.Api.Models;
using TollCents.Api.Startup;
using TollCents.Core.Entities;
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
        private readonly IApiRuntimeConfiguration _apiRuntimeConfiguration;
        private readonly TravelInformation _mockedResult = new TravelInformation
        {
            AvoidTollsRouteInformation = new RouteInformation
            {
                DistanceInMiles = 14.3,
                Description = "One road to some other road",
                DriveTime = new DriveTime
                {
                    Hours = 0,
                    Minutes = 32
                }
            },
            TollRouteInformation = new TollRouteInformation
            {
                DistanceInMiles = 15.2,
                Description = "One toll road to some other road",
                DriveTime = new DriveTime
                {
                    Hours = 0,
                    Minutes = 22
                },
                EstimatedDynamicTollPrice = 3.24,
                HasDynamicTolls = true,
                ProcessedAllDynamicTolls = false,
                GuaranteedTollPrice = 2.00
            }
        };

        public RouteInformationController(ILogger<RouteInformationController> logger, ITollInformationGateway tollInformationGateway,
            IApiRuntimeConfiguration apiRuntimeConfiguration)
        {
            _logger = logger;
            _tollInformationGateway = tollInformationGateway;
            _apiRuntimeConfiguration = apiRuntimeConfiguration;
        }

        [HttpGet]
        public async Task<ActionResult<TravelInformation>> GetTravelInformation(
            [FromQuery] string startAddress,
            [FromQuery] string endAddress,
            [FromQuery] bool hasTollTag)
        {
            if (_apiRuntimeConfiguration.MockAPIs)
            {
                return Ok(_mockedResult);
            }

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
