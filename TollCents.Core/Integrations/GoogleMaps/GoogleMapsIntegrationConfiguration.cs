namespace TollCents.Core.Integrations.GoogleMaps
{
    public interface IGoogleMapsIntegrationConfiguration
    {
        string ApiKey { get; }

        bool UseMockServices { get; }
    }

    public class GoogleMapsIntegrationConfiguration : IGoogleMapsIntegrationConfiguration
    {
        public required string ApiKey { get; init; }

        public bool UseMockServices { get; init; }
    }
}
