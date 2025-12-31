using System;
using NodaTime;
using System.Text.Json.Serialization;

namespace Guardhouse.SDK.Models;

/// <summary>
/// Represents an OAuth token response
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

/// <summary>
    /// Gets expiration time of token using NodaTime
    /// </summary>
    public Instant ExpiresAt => SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromSeconds(ExpiresIn));

    /// <summary>
    /// Gets the expiration time as DateTime for compatibility
    /// </summary>
    [JsonIgnore]
    public DateTime ExpiresAtDateTime => ExpiresAt.InUtc().ToDateTimeUtc();

    /// <summary>
    /// Checks if the token is expired or about to expire using NodaTime
    /// </summary>
    public bool IsExpired(int bufferSeconds = 60) => 
        SystemClock.Instance.GetCurrentInstant() >= ExpiresAt.Minus(Duration.FromSeconds(bufferSeconds));
}