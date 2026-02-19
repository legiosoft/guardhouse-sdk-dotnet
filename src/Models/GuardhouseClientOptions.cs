namespace Guardhouse.SDK.Models;

using System.ComponentModel.DataAnnotations;
using Constants;

/// <summary>
/// Configuration options for the Guardhouse client used to obtain access tokens.
/// </summary>
public class GuardhouseClientOptions
{
    /// <summary>
    /// The base URL of the identity server (e.g., "https://auth.example.com").
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Whether HTTPS is required for authority, token, and introspection endpoints (default: true).
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// The client ID assigned to your application by the identity server.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The client secret used to authenticate your application to the identity server.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The scope(s) to request when obtaining an access token (default: "api").
    /// </summary>
    public string Scope { get; set; } = GuardhouseConstants.Defaults.DefaultScope;

    /// <summary>
    /// Enables in-memory caching of access tokens to reduce requests to the identity server (default: true).
    /// </summary>
    public bool EnableTokenCaching { get; set; } = true;

    /// <summary>
    /// The buffer in seconds to subtract from token expiration before considering the token expired (default: 60).
    /// This ensures tokens are refreshed before they actually expire.
    /// </summary>
    public int CacheExpirationBufferSeconds { get; set; } = GuardhouseConstants.Defaults.CacheExpirationBufferSeconds;

    /// <summary>
    /// Enables automatic token refresh using refresh tokens when available (default: true).
    /// </summary>
    public bool EnableTokenRefresh { get; set; } = true;

    /// <summary>
    /// The timeout in seconds for HTTP requests to the identity server (default: 30).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = GuardhouseConstants.Defaults.RequestTimeoutSeconds;

    /// <summary>
    /// The maximum number of retry attempts for failed HTTP requests (default: 3).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = GuardhouseConstants.Defaults.MaxRetryAttempts;

    /// <summary>
    /// Enables HTTP resilience policies including retry logic with exponential backoff (default: true).
    /// </summary>
    public bool EnableHttpResilience { get; set; } = true;

    /// <summary>
    /// The client ID for introspection endpoint (optional).
    /// Use this if you need to introspect tokens from your client application for debugging purposes.
    /// If not set, the introspection will use ClientId and ClientSecret.
    /// </summary>
    public string? IntrospectionClientId { get; set; }

    /// <summary>
    /// The client secret for introspection endpoint (optional).
    /// Use this if you need to introspect tokens from your client application for debugging purposes.
    /// If not set, the introspection will use ClientId and ClientSecret.
    /// </summary>
    public string? IntrospectionClientSecret { get; set; }

    /// <summary>
    /// Enables debug logging including console output and debug-level logger messages (default: false).
    /// </summary>
    public bool EnableDebug { get; set; }

    /// <summary>
    /// How to send client credentials to the introspection endpoint (default: BasicAuth).
    /// Use FormData if your identity server does not support Basic Authentication.
    /// </summary>
    public IntrospectionCredentialTransmission IntrospectionCredentialTransmission { get; set; }
        = IntrospectionCredentialTransmission.BasicAuth;
}
