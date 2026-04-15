namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressIntegrationConfiguration
    {
        public string MetadataFilePath { get; }
        public double TollAccessPointMatchToleranceMiles { get; }
        public double NoTollTagPriceMultiplier { get; }
    }

    public class TEXpressIntegrationConfiguration : ITEXpressIntegrationConfiguration
    {
        public string MetadataFilePath { get; set; } = string.Empty;
        public double TollAccessPointMatchToleranceMiles { get; set; }
        public double NoTollTagPriceMultiplier { get; set; }
    }
}
