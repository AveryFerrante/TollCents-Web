using TollCents.Core.Integrations.TEXpress.Entities;

namespace TEXpressWebScraper
{
    internal class ApplicationConfiguration
    {
        public bool UseSelenium { get; set; }

        public bool OutputSourceHTMLFiles { get; set; }

        public string? SourceDataFilesDirectory { get; set; }

        public required string OutputFilePath { get; set; }

        public IEnumerable<TEXpressSegmentWebScraper> TEXpressSegments { get; set; } = [];
    }

    internal class TEXpressSegmentWebScraper : TEXpressSegment
    {
        public required string TEXpressCrawlerOptionsSelectValue { get; set; }
    }
}
