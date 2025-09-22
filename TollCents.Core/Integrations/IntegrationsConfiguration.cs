using TollCents.Core.Integrations.GoogleMaps;

namespace TollCents.Core.Integrations
{
    public interface IIntegrationsConfiguration
    {
        public IIntegrations? Integrations { get; }
    }

    public interface IIntegrations
    {
        public IGoogleMapsIntegrationConfiguration? GoogleMaps { get; }
    }
}
