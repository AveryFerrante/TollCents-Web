using TollCents.Core.Integrations.GoogleMaps;
using TollCents.Core.Integrations.TEXpress;

namespace TollCents.Core.Integrations
{
    public interface IIntegrationsConfiguration
    {
        public IIntegrations? Integrations { get; }
    }

    public interface IIntegrations
    {
        public IGoogleMapsIntegrationConfiguration? GoogleMaps { get; }
        public ITEXpressIntegrationConfiguration? TEXpress { get; }
    }

    public class IntegrationsConfiguration : IIntegrationsConfiguration
    {
        public Integrations? Integrations { get; set; }

        IIntegrations? IIntegrationsConfiguration.Integrations => Integrations;
    }

    public class Integrations : IIntegrations
    {
        public GoogleMapsIntegrationConfiguration? GoogleMaps { get; set; }

        public TEXpressIntegrationConfiguration? TEXpress { get; set; }

        IGoogleMapsIntegrationConfiguration? IIntegrations.GoogleMaps => GoogleMaps;

        ITEXpressIntegrationConfiguration? IIntegrations.TEXpress => TEXpress;
    }
}
