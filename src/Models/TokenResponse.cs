namespace Guardhouse.SDK.Models;

using System.Text.Json.Serialization;
using NodaTime;

/// <summary>
/// Represents the response from the token endpoint when requesting an access token.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// The access token to be used for API requests.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The type of the token (typically "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// The number of seconds until the access token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// The refresh token that can be used to obtain a new access token, if available.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// The scope(s) granted to the access token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Gets the instant when the access token expires.
    /// </summary>
    public Instant ExpiresAt => SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromSeconds(ExpiresIn));

    /// <summary>
    /// Checks if the access token is expired or close to expiring.
    /// </summary>
    /// <param name="bufferSeconds">The buffer in seconds to consider before the token is actually expired (default: 60).</param>
    /// <returns>True if the token is expired or within the buffer period, false otherwise.</returns>
    public bool IsExpired(int bufferSeconds = 60)
    {
        return SystemClock.Instance.GetCurrentInstant() >= ExpiresAt.Minus(Duration.FromSeconds(bufferSeconds));
    }
}
