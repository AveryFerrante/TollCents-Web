using TollCents.Core.Entities;
using TollCents.Core.Integrations.TEXpress.Utilities;

namespace TollCents.Core.Integrations.TEXpress.Entities
{
    public class TEXpressSegment
    {
        public required string Description { get; init; }

        public required int Identifier { get; init; }

        public required IEnumerable<CardinalDirection> CardinalDirections { get; init; }

        public IEnumerable<TollAccessEntryPoint> EntryPoints { get; init; } = [];

        public IEnumerable<TollAccessPoint> ExitPoints { get; init; } = [];

        public Dictionary<string, IEnumerable<TimePrice>> TimeOfDayPricing { get; set; } = new();
    }

    public class TollAccessPoint
    {
        public string? Description { get; init; }

        public Coordinate Location { get; init; }

    }

    public class TollAccessEntryPoint : TollAccessPoint
    {
        public Coordinate? SkipWaypoint { get; init; }

        /// <summary>
        /// Based on the segment's cardinal direction(s), the order number indicates the 
        /// sequence of this entry point relative to other entry points on the same segment. 
        /// </summary>
        public int? SequenceNumber { get; init; }
    }

    public struct TimePrice
    {
        public string Time { get; init; }

        public double Price { get; init; }
    }
}
