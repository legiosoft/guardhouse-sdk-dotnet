namespace Guardhouse.SDK.Models;

/// <summary>
/// Configuration options for Guardhouse external API operations.
/// </summary>
public class GuardhouseUserOptions
{
    /// <summary>
    /// Optional base URL for the Guardhouse external API.
    /// If omitted, the SDK falls back to GuardhouseClientOptions.Authority.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Enables debug logging for outgoing API requests (default: false).
    /// </summary>
    public bool EnableDebug { get; set; }
}
