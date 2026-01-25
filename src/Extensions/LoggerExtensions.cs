namespace Guardhouse.SDK.Extensions;

using Microsoft.Extensions.Logging;

internal static class LoggerExtensions
{
    public static void LogDebugIf(
        this ILogger logger,
        bool condition,
        string message,
        params object?[] args)
    {
        if (condition)
        {
            logger.LogDebug(message, args);
        }
    }
}
