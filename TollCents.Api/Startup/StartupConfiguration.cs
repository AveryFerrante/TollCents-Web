using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using TollCents.Api.Authentication;
using TollCents.Core.Integrations;

namespace TollCents.Api.Startup
{
    public static class StartupConfiguration
    {
        public static IServiceCollection ConfigureApplication(this IServiceCollection services, IConfiguration configuration)
        {
            var applicationConfiguration = configuration.Get<ApplicationConfiguration>();
            ArgumentNullException.ThrowIfNull(applicationConfiguration, nameof(applicationConfiguration));

            services
                .AddSingleton<IIntegrationsConfiguration>(applicationConfiguration)
                .AddMemoryCache()
                .AddSingleton<IAccessCodeValidationService, AccessCodeValidationService>();

            services.ConfigureCorsPolicies();
            services.ConfigureRateLimiting(applicationConfiguration);
            services.AddAuthentication("AccessCodeScheme")
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("AccessCodeScheme", null);

            return services;
        }

        private static IServiceCollection ConfigureCorsPolicies(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(ConfigurationConstants.DevCORSPolicyName, builder =>
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
                options.AddPolicy(ConfigurationConstants.ProductionCORSPolicyName, builder =>
                    builder
                        .WithOrigins(ConfigurationConstants.AllowedTollCentsDomains)
                        .AllowAnyHeader()
                        .AllowAnyMethod());
            });
            return services;
        }

        private static IServiceCollection ConfigureRateLimiting(this IServiceCollection services, ApplicationConfiguration appConfiguration)
        {
            var rateLimitConfiguration = appConfiguration?.RateLimiterConfiguration;
            ArgumentNullException.ThrowIfNull(rateLimitConfiguration, nameof(appConfiguration.RateLimiterConfiguration));

            if (rateLimitConfiguration.Enabled)
            {
                services.AddRateLimiter(rateLimiterOptions =>
                    {
                        rateLimiterOptions.GlobalLimiter = FixedWindowRateLimitingPolicy(rateLimitConfiguration);
                        rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    }); 
            }
            return services;
        }

        private static PartitionedRateLimiter<HttpContext> FixedWindowRateLimitingPolicy(IRateLimiterConfigurationOptions options)
        {
            return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = GetRateLimiterPartitionKey(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = options.PermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(options.WindowInMinutes)
                    });
            });
        }

        private static string GetRateLimiterPartitionKey(HttpContext context)
        {
            // TODO: Use a custom attribute on the controller and get the resolved controller class to check attribute for "public endpoint"
            // var endpoint = context.GetEndpoint();
            // bool isPublicEndpoint = endpoint?.Metadata.GetMetadata<PublicEndpointAttribute>() != null;
            var path = context.Request.Path.Value;

            bool isPublicEndpoint = path?.Contains("access-code/validate", StringComparison.OrdinalIgnoreCase) == true;

            string partitionKey;

            if (isPublicEndpoint)
            {
                partitionKey = context.Connection?.RemoteIpAddress?.ToString() ?? "unknown-ip";
            }
            else
            {
                var authHeader = context.Request.Headers["X-Access-Code"].ToString();
                partitionKey = string.IsNullOrWhiteSpace(authHeader) ? "unauthenticated" : authHeader;
            }

            return partitionKey;
        }
    }
}
