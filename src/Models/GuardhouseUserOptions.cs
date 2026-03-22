namespace Guardhouse.SDK.Models;

/// <summary>
/// Configuration options for Guardhouse user-management API operations.
/// </summary>
public class GuardhouseUserOptions
{
    /// <summary>
    /// Optional base URL for the Guardhouse API.
    /// If omitted, the SDK falls back to GuardhouseClientOptions.Authority.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Enables debug logging for outgoing user API requests (default: false).
    /// </summary>
    public bool EnableDebug { get; set; }
}
