using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using TollCents.Api.Startup;

namespace TollCents.Api.Authentication
{
    public class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAccessCodeValidationService accessCodeValidationService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        private readonly IAccessCodeValidationService _accessCodeValidationService = accessCodeValidationService;
        private readonly ILogger<HeaderAuthenticationHandler> _logger = logger.CreateLogger<HeaderAuthenticationHandler>();

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var headerValue = Request.Headers[ConfigurationConstants.ApiKeyHeaderName].FirstOrDefault();
            if (await _accessCodeValidationService.IsValidAccessCode(headerValue))
            {
                _logger.LogInformation("Access code {AccessCode} passed authentication", headerValue);
                var claims = new[] { new Claim(ClaimTypes.Name, "AccessCodeUser") };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            return AuthenticateResult.Fail($"Failed header authentication from access code \"{headerValue}\"");
        }
    }
}
