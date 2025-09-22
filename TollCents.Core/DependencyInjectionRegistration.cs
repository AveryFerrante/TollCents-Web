using GoogleApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations.GoogleMaps;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        public static IServiceCollection RegisterIntegrations(this IServiceCollection services)
        {
            services.AddScoped<ITollInformationGateway, TollInformationGateway>();
            services.AddScoped<IAddressLookupGateway, AddressLookupGateway>();
            services.AddGoogleApiClients();

            return services;
        }
    }
}
