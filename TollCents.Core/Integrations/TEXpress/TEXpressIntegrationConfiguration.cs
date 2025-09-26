namespace TollCents.Core.Integrations.TEXpress
{
    public interface ITEXpressIntegrationConfiguration
    {
        public string? TEXpressDataFilePath { get; }
        public double? TollAccessPointMatchToleranceMiles { get; }
    }
}
