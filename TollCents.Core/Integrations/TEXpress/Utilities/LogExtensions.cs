using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace TollCents.Core.Integrations.TEXpress.Utilities
{
    internal static partial class LogExtensions
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "No TEXpress info found using file {FilePath}")]
        public static partial void LogNoTEXpressSegments(this ILogger logger, string filePath);
    }
}
