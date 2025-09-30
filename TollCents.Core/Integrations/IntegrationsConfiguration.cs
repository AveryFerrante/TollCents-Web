using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core.Integrations
{
    public interface IIntegrationsConfiguration
    {
        public IIntegrations? Integrations { get; }
    }

    public interface IIntegrations : ITEXpressIntegrationConfiguration
    {
        public IGoogleMapsIntegrationConfiguration? GoogleMaps { get; }
    }
}
