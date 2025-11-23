using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using TollCents.Api.Models.Attributes;

namespace TollCents.Api.Authentication
{
    public class SwaggerAccessCodeOption : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var isPublicEndpoint = context.MethodInfo.GetCustomAttribute<PublicEndpointAttribute>();
            if (isPublicEndpoint != null)
            {
                return;
            }

            operation.Parameters ??= new List<OpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Access-Code",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = "string" },
                Description = "Access code required for authentication"
            });
        }
    }
}
