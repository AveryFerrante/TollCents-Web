using GoogleApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services, IIntegrationsConfiguration integrationsConfig)
        {
            ArgumentNullException.ThrowIfNull(integrationsConfig, nameof(integrationsConfig));
            if (integrationsConfig.Integrations?.GoogleMaps?.UseMockServices ??
                throw new ArgumentNullException(nameof(IGoogleMapsIntegrationConfiguration.UseMockServices)))
            {
                services.AddScoped<ITollInformationGateway, TollInformationGatewayMock>();
                services.AddScoped<IAddressLookupGateway, AddressLookupGatewayMock>();
            }
            else
            {
                services.AddScoped<ITollInformationGateway, TollInformationGateway>();
                services.AddScoped<IAddressLookupGateway, AddressLookupGateway>();
            }

            services.AddSingleton(integrationsConfig);
            services.AddGoogleApiClients();
            services.AddScoped<ITEXpressTollPriceCalculator, TEXpressTollPriceCalculator>();
            services.AddMemoryCache();

            return services;
        }

        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services, IConfiguration configuration)
        {
            var integrationsConfiguration = configuration.Get<IntegrationsConfiguration>();
            ArgumentNullException.ThrowIfNull(integrationsConfiguration, nameof(configuration));
            return services.RegisterGoogleMapsIntegration(integrationsConfiguration);
        }
    }
}
