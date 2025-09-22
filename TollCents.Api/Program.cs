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

            // Add services to the container.
            builder.Services.ConfigureApplication(builder.Configuration);
            builder.Services.RegisterIntegrations();
            builder.Services.AddControllers();
            // C:\\Users\\Avery\\Desktop\\WebTest\\test-log.txt
            builder.Services.AddLogging(b =>
            {
                var logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File("/var/www/tollcents/logs/api.log",
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
                b.AddSerilog(logger);
            });

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

            app.MapControllers();

            app.Run();
        }
    }
}
