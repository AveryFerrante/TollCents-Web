using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TollCents.Core.Integrations.GoogleMaps.Requests
{
    public class ByAddressRequest
    {
        public required string StartAddress { get; set; }
        public required string EndAddress { get; set; }
        public bool? IncludeTollPass { get; set; } = false;
    }
}
