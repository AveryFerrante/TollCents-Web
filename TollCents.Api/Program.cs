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
            builder.Services.RegisterGoogleMapsIntegration(builder.Configuration.GetValue<bool>("MockIntegrations"));
            builder.Services.AddControllers();
            builder.Services.AddSwaggerDefinition();
            
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseCors(ConfigurationConstants.DevCORSPolicyName);
            }
            else
            {
                app.UseCors(ConfigurationConstants.ProductionCORSPolicyName);
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (httpContext.Response.StatusCode >= 400)
                        return Serilog.Events.LogEventLevel.Warning;
                    return Serilog.Events.LogEventLevel.Information;
                };
            });

            app.MapControllers();

            app.Run();
        }
    }
}
