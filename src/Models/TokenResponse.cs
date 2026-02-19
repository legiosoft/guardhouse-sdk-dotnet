namespace Guardhouse.SDK.Models;

using System.Text.Json.Serialization;
using NodaTime;

/// <summary>
/// Represents the response from the token endpoint when requesting an access token.
/// </summary>
public record TokenResponse
{
    /// <summary>
    /// The access token to be used for API requests.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// The type of the token (typically "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    /// <summary>
    /// The number of seconds until the access token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The refresh token that can be used to obtain a new access token, if available.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// The number of seconds until the refresh token expires, if provided by the identity server.
    /// </summary>
    [JsonPropertyName("refresh_token_expires_in")]
    public int? RefreshTokenExpiresIn { get; init; }

    /// <summary>
    /// The scope(s) granted to the access token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    private IClock? _clock;

    /// <summary>
    /// Gets the instant when the access token expires.
    /// </summary>
    public Instant ExpiresAt => (_clock ?? SystemClock.Instance).GetCurrentInstant().Plus(Duration.FromSeconds(ExpiresIn));

    /// <summary>
    /// Checks if the access token is expired or close to expiring.
    /// </summary>
    /// <param name="bufferSeconds">The buffer in seconds to consider before the token is actually expired (default: 60).</param>
    /// <returns>True if the token is expired or within the buffer period, false otherwise.</returns>
    public bool IsExpired(int bufferSeconds = 60)
    {
        return (_clock ?? SystemClock.Instance).GetCurrentInstant() >= ExpiresAt.Minus(Duration.FromSeconds(bufferSeconds));
    }

    /// <summary>
    /// Sets the clock for testability (primarily used in unit tests).
    /// </summary>
    /// <param name="clock">The clock to use for time calculations.</param>
    /// <returns>This instance for method chaining.</returns>
    public TokenResponse WithClock(IClock clock)
    {
        _clock = clock;
        return this;
    }
}
