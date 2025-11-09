using Serilog;
using TollCents.Api.Authentication;
using TollCents.Api.Startup;
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
            builder.Services.RegisterGoogleMapsIntegration();
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerAccessCodeOption>();
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseCors(ConfigurationConstants.DevCORSPolicyName);
                app.UseRateLimiter();
            }
            else
            {
                app.UseCors(ConfigurationConstants.ProductionCORSPolicyName);
                app.UseRateLimiter();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
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
