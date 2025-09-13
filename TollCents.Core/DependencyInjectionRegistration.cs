using GoogleApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations.GoogleMaps;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        public static IServiceCollection RegisterIntegrations(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ITollInformationGateway, TollInformationGateway>();
            services.AddScoped<IAddressLookupGateway, AddressLookupGateway>();
            services.AddGoogleApiClients();

            // TODO: Should this really be responsible? Relies on knowing how the app has its configuration set up.
            var config = new GoogleMapsIntegrationConfiguration();
            var abc = configuration.GetSection("Integrations:Google");
            configuration.GetSection("Integrations:Google").Bind(config);
            services.AddSingleton<IGoogleMapsIntegrationConfiguration>(config);

            return services;
        }
    }
}
