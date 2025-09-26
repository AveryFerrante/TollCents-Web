using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TollCents.Core.Integrations.TEXpress.Entities;

namespace TEXpressWebScraper
{
    public class TEXpressSegmentWebScraper : TEXpressSegment
    {
        public required string TEXpressCrawlerOptionsSelectValue { get; set; }
    }
}
