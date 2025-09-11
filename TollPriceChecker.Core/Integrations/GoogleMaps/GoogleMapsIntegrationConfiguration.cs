namespace TollPriceChecker.Core.Integrations.GoogleMaps
{
    public interface IGoogleMapsIntegrationConfiguration
    {
        string? ApiKey { get; }
    }

    public class GoogleMapsIntegrationConfiguration : IGoogleMapsIntegrationConfiguration
    {
        public string? ApiKey { get; set; }
    }
}
