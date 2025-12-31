using System.ComponentModel.DataAnnotations;

namespace Guardhouse.SDK.Models;

/// <summary>
/// Configuration for Guardhouse SDK client functionality
/// </summary>
public class GuardhouseClientOptions
{
    /// <summary>
    /// The Guardhouse server base URL
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Client ID for authentication
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for authentication
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Scope to request (default: "api")
    /// </summary>
    public string Scope { get; set; } = "api";

    /// <summary>
    /// Enable token caching in memory
    /// </summary>
    public bool EnableTokenCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration buffer in seconds (default: 60 seconds before actual expiration)
    /// </summary>
    public int CacheExpirationBufferSeconds { get; set; } = 60;

    /// <summary>
    /// Enable automatic token refresh using refresh tokens
    /// </summary>
    public bool EnableTokenRefresh { get; set; } = true;

    /// <summary>
    /// HTTP request timeout in seconds (default: 30)
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for HTTP requests (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Enable HTTP resilience pipeline with retry and timeout
    /// </summary>
    public bool EnableHttpResilience { get; set; } = true;
}