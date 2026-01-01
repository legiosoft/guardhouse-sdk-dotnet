using System.Text.Json.Serialization;
using NodaTime;

namespace Guardhouse.SDK.Models;

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

    public Instant ExpiresAt => SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromSeconds(ExpiresIn));

    public bool IsExpired(int bufferSeconds = 60) => 
        SystemClock.Instance.GetCurrentInstant() >= ExpiresAt.Minus(Duration.FromSeconds(bufferSeconds));
}
