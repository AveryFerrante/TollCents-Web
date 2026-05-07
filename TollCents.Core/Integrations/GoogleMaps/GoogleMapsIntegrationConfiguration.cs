using System.ComponentModel.DataAnnotations;

namespace TollCents.Core.Integrations.GoogleMaps
{
    public class GoogleMapsIntegrationConfiguration
    {
        public const string SectionName = "GoogleMaps";

        [Required]
        public required string ApiKey { get; init; }

        public bool UseMockServices { get; init; } = false;
    }
}
