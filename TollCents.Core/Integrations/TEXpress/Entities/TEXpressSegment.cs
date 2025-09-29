using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TollCents.Core.Integrations.TEXpress.Entities
{
    public class TEXpressSegment
    {
        public string? Description { get; set; }
        public IEnumerable<TollAccessPoint> EntryPoints { get; set; } = new List<TollAccessPoint>();
        public IEnumerable<TollAccessPoint> ExitPoints { get; set; } = new List<TollAccessPoint>();
        public Dictionary<string, IEnumerable<TimePrice>> TimeOfDayPricing { get; set; } = new Dictionary<string, IEnumerable<TimePrice>>();
    }

    public class TollAccessPoint
    {
        public string? Description { get; set; }
        public Coordinate Location { get; set; }
    }
    public struct Coordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public struct TimePrice
    {
        public string Time { get; set; }
        public double Price { get; set; }
    }
}
