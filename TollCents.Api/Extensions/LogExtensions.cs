namespace TollCents.Api.Extensions
{
    internal static partial class LogExtensions
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Validating access code {AccessCode}")]
        internal static partial void LogAccessCode(this ILogger logger, string? accessCode);

        [LoggerMessage(LogLevel.Information, Message = "Access code {AccessCode} passed authentication")]
        internal static partial void LogValidAccessCode(this ILogger logger, string? accessCode);

        [LoggerMessage(LogLevel.Warning, Message = "Rate limit exceeded for partition key: {RateLimitPartitionKey}")]
        internal static partial void LogRateLimitRejection(this ILogger logger, string? rateLimitPartitionKey);

        [LoggerMessage(LogLevel.Warning, Message = "Access code {AccessCode} failed authentication")]
        internal static partial void LogInvalidAccessCode(this ILogger logger, string? accessCode);
    }
}
