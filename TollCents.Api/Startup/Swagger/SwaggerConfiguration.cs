using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using TollCents.Api.Authentication;
using TollCents.Api.Models.Attributes;

namespace TollCents.Api.Startup.Swagger
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerDefinition(this IServiceCollection services)
        {
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "TollCents API", Version = "v1" });

                const string schemeName = "ApiKeyScheme";
                var apiKeyScheme = new OpenApiSecurityScheme
                {
                    Description = $"Api key authentication using the '{ConfigurationConstants.ApiKeyHeaderName}' header",
                    Name = ConfigurationConstants.ApiKeyHeaderName,
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = schemeName,
                };

                options.AddSecurityDefinition(schemeName, apiKeyScheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = schemeName
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                options.OperationFilter<ExcludeSecurityOnPublicEndpoints>();
            });

            return services;
        }
    }

    public class ExcludeSecurityOnPublicEndpoints : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // TODO: It seems Swagger still sends the api header for public endpoints.
            // Not a problem now, but maybe in the future
            var isPublicEndpoint = context.MethodInfo.GetCustomAttribute<PublicEndpointAttribute>();
            if (isPublicEndpoint != null)
            {
                operation.Security.Clear();
            }
        }
    }
}
