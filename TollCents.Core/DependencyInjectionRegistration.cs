using GoogleApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services, bool useMockServices)
        {
            
            if (useMockServices)
            {
                services.AddScoped<ITollInformationGateway, TollInformationGatewayMock>();
                services.AddScoped<IAddressLookupGateway, AddressLookupGatewayMock>();
            }
            else
            {
                services.AddScoped<ITollInformationGateway, TollInformationGateway>();
                services.AddScoped<IAddressLookupGateway, AddressLookupGateway>();
            }
            
            services.AddGoogleApiClients();
            services.AddScoped<ITEXpressTollPriceCalculator, TEXpressTollPriceCalculator>();
            services.AddMemoryCache();

            return services;
        }
    }
}
