using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using TollCents.Api.Authentication;
using TollCents.Api.Models.Attributes;
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
                        rateLimiterOptions.OnRejected = (context, _) =>
                        {
                            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                            var logger = loggerFactory.CreateLogger("RateLimiting");
                            var rateLimitPartitionKey = GetRateLimiterPartitionKey(context.HttpContext);
                            logger.LogWarning("Rate limit exceeded for partition key: {RateLimitPartitionKey}", rateLimitPartitionKey);
                            return ValueTask.CompletedTask;
                        };
                    }); 
            }
            return services;
        }

        private static PartitionedRateLimiter<HttpContext> FixedWindowRateLimitingPolicy(IRateLimiterConfigurationOptions options)
        {
            return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                bool isPublicEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<PublicEndpointAttribute>() != null;
                if (isPublicEndpoint)
                {
                    return RateLimitPartition.GetNoLimiter("public-endpoint");
                }

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
            // Auth middleware is registered before rate limiting, meaning this should
            // always exist, else the request would be rejected before reaching here.
            return context.Request.Headers["X-Access-Code"].ToString()
                ?? throw new InvalidOperationException("No Access Header Code Found");
        }
    }
}
