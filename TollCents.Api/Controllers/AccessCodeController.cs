using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TollCents.Api.Authentication;
using TollCents.Api.Models;

namespace TollCents.Api.Controllers
{
    [ApiController]
    [Route("api/access-code")]
    public class AccessCodeController : ControllerBase
    {
        private readonly IAccessCodeValidationService _accessCodeValidationService;

        public AccessCodeController(IAccessCodeValidationService accessCodeValidationService)
        {
            _accessCodeValidationService = accessCodeValidationService;
        }

        [HttpGet("validate")]
        public async Task<ActionResult<AccessCodeValidity>> ValidateAccessCode([FromQuery] string accessCode)
        {
            var isValid = await _accessCodeValidationService.IsValidAccessCode(accessCode);
            var response = new AccessCodeValidity
            {
                IsValidAccessCode = isValid
            };
            return Ok(response);
        }
    }
}
