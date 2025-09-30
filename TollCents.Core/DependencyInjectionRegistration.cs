using GoogleApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services)
        {
            services.AddScoped<ITollInformationGateway, TollInformationGateway>();
            services.AddScoped<IAddressLookupGateway, AddressLookupGateway>();
            services.AddGoogleApiClients();

            services.AddScoped<ITEXpressTollPriceCalculator, TEXpressTollPriceCalculator>();
            // TODO: Add memory cache from here?

            return services;
        }
    }
}
