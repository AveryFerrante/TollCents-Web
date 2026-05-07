using GoogleApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        /// <param name="configuration">Configuration instance containing necessary values</param>
        /// <returns></returns>
        public static IServiceCollection RegisterGoogleMapsIntegration(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<GoogleMapsIntegrationConfiguration>(
                configuration.GetSection(GoogleMapsIntegrationConfiguration.SectionName));
            services.AddOptionsWithValidateOnStart<GoogleMapsIntegrationConfiguration>();
            
            bool useMockServices = configuration.GetValue<bool>(
                $"{GoogleMapsIntegrationConfiguration.SectionName}:{nameof(GoogleMapsIntegrationConfiguration.UseMockServices)}");
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
            services.RegisterTEXpressTollCalculatorIntegration(configuration);

            return services;
        }

        /// <summary>
        /// Registers necessary services & configurations to run the TEXpressTollCalculator integration.
        /// This is automatically registered when calling <see cref="RegisterGoogleMapsIntegration">RegisterGoogleMapsIntegration</see>, 
        /// but can be called directly if needed.
        /// </summary>
        /// <param name="services">IServiceCollection instnace.</param>
        /// <param name="configuration">Configuration instance containing necessary values</param>
        /// <returns><see cref="IServiceCollection"/></returns>
        public static IServiceCollection RegisterTEXpressTollCalculatorIntegration(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<TEXpressIntegrationConfiguration>(
                configuration.GetSection(TEXpressIntegrationConfiguration.SectionName));
            services.AddOptionsWithValidateOnStart<TEXpressIntegrationConfiguration>();

            var analysisEnabled = configuration.GetValue<bool>(
                $"{TEXpressIntegrationConfiguration.SectionName}:{nameof(TEXpressIntegrationConfiguration.AnalysisModeEnabled)}");
            if (analysisEnabled)
            {
                services.AddMediatR(config =>
                {
                    config.RegisterServicesFromAssembly(typeof(TEXpressAnalysisEventStream<,>).Assembly);
                    config.AddOpenBehavior(typeof(TEXpressAnalysisEventStream<,>));
                }); 
            }

            services.AddScoped<ITEXpressTollPriceCalculator, TEXpressTollPriceCalculator>();
            services.AddScoped<ITEXpressSegmentSkipAnomolies, TEXpressSegmentSkipAnomolies>();
            services.AddMemoryCache();

            return services;
        }
    }
}
