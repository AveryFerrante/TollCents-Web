
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using TollCents.Api.Authentication;
using TollCents.Core;

namespace TollCents.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.RegisterIntegrations(builder.Configuration);
            builder.Services.AddSingleton<IAccessCodeValidationService, AccessCodeValidationService>();
            builder.Services.AddMemoryCache();

            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection?.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 10,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(10)
                        }));
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder => builder.AllowAnyOrigin()
                                      .AllowAnyMethod()
                                      .AllowAnyHeader());
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerAccessCodeOption>();
            });
            builder.Services.AddAuthentication("AccessCodeScheme")
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("AccessCodeScheme", null);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            if (app.Environment.IsDevelopment())
            {
                // TODO: Add production CORS policy
                app.UseCors("AllowAllOrigins");
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseRateLimiter(); 
            }

            app.MapControllers();

            app.Run();
        }
    }
}
