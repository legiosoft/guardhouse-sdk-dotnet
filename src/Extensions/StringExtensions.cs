namespace Guardhouse.SDK.Extensions;

/// <summary>
/// Provides safe string extension methods to avoid exceptions when manipulating strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Safely gets a substring of specified length, avoiding exceptions if string is null or shorter than requested length.
    /// </summary>
    /// <param name="value">The string to get a substring from.</param>
    /// <param name="length">The maximum length of substring (must be non-negative).</param>
    /// <returns>A safe substring, or an empty string if input is null or empty.</returns>
    public static string SafeSubstring(this string? value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (length < 0)
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value[..length];
    }

    /// <summary>
    /// Gets a truncated string with an ellipsis if it exceeds specified maximum length.
    /// The result may be up to maxLength + ellipsis.Length characters long.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">The maximum length to allow before adding ellipsis (must be non-negative).</param>
    /// <param name="ellipsis">The ellipsis string to append if truncation occurs (default: "...").</param>
    /// <returns>The original string if within maxLength, otherwise truncated with ellipsis appended.</returns>
    public static string GetPreview(this string? value, int maxLength = 20, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (maxLength < 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + ellipsis;
    }
}
