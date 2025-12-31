using System;
using NodaTime;
using System.Text.Json.Serialization;

namespace Guardhouse.SDK.Models;

/// <summary>
/// Represents a token introspection response
/// </summary>
public class IntrospectionResponse
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("exp")]
    public long? Exp { get; set; }

    [JsonPropertyName("iat")]
    public long? Iat { get; set; }

    [JsonPropertyName("nbf")]
    public long? Nbf { get; set; }

    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    [JsonPropertyName("jti")]
    public string? Jti { get; set; }

    /// <summary>
    /// Gets the expiration time as NodaTime Instant
    /// </summary>
    public Instant? ExpiresAt => Exp.HasValue ? Instant.FromUnixTimeSeconds(Exp.Value) : null;

    /// <summary>
    /// Gets the expiration time as DateTime for compatibility
    /// </summary>
    [JsonIgnore]
    public DateTime? ExpiresAtDateTime => ExpiresAt?.InUtc().ToDateTimeUtc();

    /// <summary>
    /// Gets the issued at time as NodaTime Instant
    /// </summary>
    public Instant? IssuedAt => Iat.HasValue ? Instant.FromUnixTimeSeconds(Iat.Value) : null;

    /// <summary>
    /// Gets the not before time as NodaTime Instant
    /// </summary>
    public Instant? NotBefore => Nbf.HasValue ? Instant.FromUnixTimeSeconds(Nbf.Value) : null;
}