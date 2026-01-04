namespace Guardhouse.SDK.Extensions;

/// <summary>
/// Provides safe string extension methods to avoid exceptions when manipulating strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Safely gets a substring of specified length, avoiding exceptions if the string is null or shorter than requested length.
    /// </summary>
    /// <param name="value">The string to get a substring from.</param>
    /// <param name="length">The maximum length of the substring.</param>
    /// <returns>A safe substring, or an empty string if the input is null or empty.</returns>
    public static string SafeSubstring(this string? value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value[..length];
    }

    /// <summary>
    /// Gets a substring preview with an ellipsis if the string exceeds the specified length.
    /// </summary>
    /// <param name="value">The string to get a preview from.</param>
    /// <param name="maxLength">The maximum length of the preview.</param>
    /// <param name="ellipsis">The ellipsis string to append (default: "...").</param>
    /// <returns>A preview string with an ellipsis if it exceeds the maximum length.</returns>
    public static string GetPreview(this string? value, int maxLength = 20, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(value))
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
