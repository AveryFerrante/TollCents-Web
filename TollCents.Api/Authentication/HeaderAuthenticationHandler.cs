using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace TollCents.Api.Authentication
{
    public class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IAccessCodeValidationService _accessCodeValidationService;
        public HeaderAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IAccessCodeValidationService accessCodeValidationService)
            : base(options, logger, encoder)
        {
            _accessCodeValidationService = accessCodeValidationService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var headerValue = Request.Headers["X-Access-Code"].FirstOrDefault();
            if (await _accessCodeValidationService.IsValidAccessCode(headerValue))
            {
                var claims = new[] { new Claim(ClaimTypes.Name, "AccessCodeUser") };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            return AuthenticateResult.Fail("Invalid access code");
        }
    }
}
