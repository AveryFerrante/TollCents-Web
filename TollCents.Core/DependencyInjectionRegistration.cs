using GoogleApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TollCents.Core.Integrations;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;
using TollCents.Core.Integrations.TEXpress.Services;

namespace TollCents.Core
{
    public static class DependencyInjectionRegistration
    {
        /// <summary>
        /// Registers necessary services & configurations to run the GoogleMaps integration.
        /// </summary>
        /// <param name="services">IServiceCollection instnace.</param>
        /// <param name="configSection">Configuration section containing necessary values</param>
        /// <returns></returns>
        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services,
            IConfigurationSection configSection)
        {
            services.Configure<GoogleMapsIntegrationConfiguration>(configSection);
            services.AddOptionsWithValidateOnStart<GoogleMapsIntegrationConfiguration>();
            bool useMockServices = configSection.GetValue<bool>(nameof(GoogleMapsIntegrationConfiguration.UseMockServices));

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

            return services;
        }

        /// <summary>
        /// Registers necessary services & configurations to run the TEXpressTollCalculator integration.
        /// </summary>
        /// <param name="services">IServiceCollection instnace.</param>
        /// <param name="configSection">Configuration section containing necessary values</param>
        /// <returns></returns>
        public static IServiceCollection RegisterTEXpressTollCalculatorIntegration(this IServiceCollection services,
            IConfigurationSection configSection)
        {
            services.Configure<TEXpressIntegrationConfiguration>(configSection);
            services.AddOptionsWithValidateOnStart<TEXpressIntegrationConfiguration>();
            services.AddScoped<ITEXpressTollPriceCalculator, TEXpressTollPriceCalculator>();
            services.AddScoped<ITEXpressSegmentSkipAnomolies, TEXpressSegmentSkipAnomolies>();
            services.AddMemoryCache();

            return services;
        }
    }
}
