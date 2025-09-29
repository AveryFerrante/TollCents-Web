using TollCents.Core.Integrations;
using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Api.Startup
{
    // Root configuration class
    public class ApplicationConfiguration : IIntegrationsConfiguration, IRateLimiterConfiguration
    {
        public Integrations? Integrations { get; set; }
        public RateLimiterConfiguration? RateLimiterConfiguration { get; set; }

        // Explicit interface implementations (required for interface contracts)
        IIntegrations? IIntegrationsConfiguration.Integrations => Integrations;
        IRateLimiterConfigurationOptions? IRateLimiterConfiguration.RateLimiterConfiguration => RateLimiterConfiguration;
    }

    // Integrations configuration
    public class Integrations : IIntegrations
    {
        public GoogleMapsIntegrationConfiguration? GoogleMaps { get; set; }

        public string? TEXpressDataFilePath { get; set; }

        public double? TollAccessPointMatchToleranceMiles { get; set; }

        public double? NoTollTagPriceMultiplier { get; set; }

        IGoogleMapsIntegrationConfiguration? IIntegrations.GoogleMaps => GoogleMaps;
    }

    // Google Maps configuration
    public class GoogleMapsIntegrationConfiguration : IGoogleMapsIntegrationConfiguration
    {
        public string? ApiKey { get; set; }
    }

    // Rate limiter configuration
    public class RateLimiterConfiguration : IRateLimiterConfigurationOptions
    {
        public bool Enabled { get; set; }
        public int PermitLimit { get; set; }
        public int WindowInMinutes { get; set; }
    }

    // Interface for rate limiter configuration
    public interface IRateLimiterConfiguration
    {
        IRateLimiterConfigurationOptions? RateLimiterConfiguration { get; }
    }

    // Interface for rate limiter options
    public interface IRateLimiterConfigurationOptions
    {
        bool Enabled { get; }
        int PermitLimit { get; }
        int WindowInMinutes { get; }
    }
}
