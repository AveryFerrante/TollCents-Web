namespace TollCents.Core.Integrations.TEXpress
{
    public class TEXpressIntegrationConfiguration
    {
        public const string SectionName = "TEXpress";
        public string MetadataFilePath { get; init; } = string.Empty;

        public double TollAccessPointMatchToleranceMiles { get; init; } = 0.05;

        public double NoTollTagPriceMultiplier { get; init; } = 1;

        public bool AnalysisModeEnabled { get; init; } = false;
    }
}
