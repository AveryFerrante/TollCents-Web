namespace TollCents.Api.Startup
{
    // Root configuration class
    public class ApplicationConfiguration : IRateLimiterConfiguration
    {
        public RateLimiterConfiguration? RateLimiterConfiguration { get; set; }

        IRateLimiterConfigurationOptions? IRateLimiterConfiguration.RateLimiterConfiguration => RateLimiterConfiguration;
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
