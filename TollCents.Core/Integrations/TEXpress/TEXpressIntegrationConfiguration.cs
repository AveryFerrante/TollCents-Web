namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressIntegrationConfiguration
    {
        public string MetadataFilePath { get; }
        public double TollAccessPointMatchToleranceMiles { get; }
        public double NoTollTagPriceMultiplier { get; }
    }
}
