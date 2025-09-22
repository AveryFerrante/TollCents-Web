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
        private readonly ILogger<AccessCodeController> _logger;
        private readonly IAccessCodeValidationService _accessCodeValidationService;

        public AccessCodeController(ILogger<AccessCodeController> logger, IAccessCodeValidationService accessCodeValidationService)
        {
            _logger = logger;
            _accessCodeValidationService = accessCodeValidationService;
        }

        [HttpPost("validate")]
        public async Task<ActionResult<AccessCodeValidity>> ValidateAccessCode([FromBody] string accessCode)
        {
            _logger.LogInformation("Validating access code.");
            var isValid = await _accessCodeValidationService.IsValidAccessCode(accessCode);
            var response = new AccessCodeValidity
            {
                IsValidAccessCode = isValid
            };
            return Ok(response);
        }
    }
}
