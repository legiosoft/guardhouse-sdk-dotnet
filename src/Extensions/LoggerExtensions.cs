namespace Guardhouse.SDK.Extensions;

using System.Globalization;
using System.Text;
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
            logger.LogDebug(SanitizeForLog(message), SanitizeLogArguments(args));
        }
    }

    private static object?[] SanitizeLogArguments(object?[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        object?[]? sanitizedArgs = null;
        for (var i = 0; i < args.Length; i++)
        {
            var sanitizedArg = SanitizeLogArgument(args[i]);
            if (!ReferenceEquals(sanitizedArg, args[i]))
            {
                sanitizedArgs ??= (object?[])args.Clone();
                sanitizedArgs[i] = sanitizedArg;
            }
        }

        return sanitizedArgs ?? args;
    }

    private static object? SanitizeLogArgument(object? value)
    {
        return value is string ? "[REDACTED]" : value;
    }

    private static string SanitizeForLog(string value)
    {
        StringBuilder? builder = null;

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            var replacement = GetLogSafeReplacement(current);
            if (replacement == null)
            {
                builder?.Append(current);
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
            builder.Append(replacement);
        }

        return builder?.ToString() ?? value;
    }

    private static string? GetLogSafeReplacement(char value)
    {
        return value switch
        {
            '\r' => @"\r",
            '\n' => @"\n",
            '\t' => @"\t",
            '\u2028' => @"\u2028",
            '\u2029' => @"\u2029",
            _ when char.IsControl(value) => @"\u" + ((int)value).ToString("X4", CultureInfo.InvariantCulture),
            _ => null
        };
    }
}
