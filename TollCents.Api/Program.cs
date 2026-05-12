using Serilog;
using TollCents.Api.Startup;
using TollCents.Api.Startup.Swagger;
using TollCents.Core;

namespace TollCents.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services));

            // Add services to the container.
            builder.Services.ConfigureApplication(builder.Configuration);
            builder.Services.RegisterGoogleMapsIntegration(builder.Configuration.GetSection("Integrations"));

            builder.Services.AddControllers();
            builder.Services.AddSwaggerDefinition();

            // TODO: Update nginx routing for better SPA support (don't need specific /route type forwarding,
            // just use the "/" route fallback to index.html everytime for client side routing).
            var app = builder.Build();
            app.ConfigureOrderedRequestPipeline();
            app.MapControllers();
            app.Run();
        }
    }
}
