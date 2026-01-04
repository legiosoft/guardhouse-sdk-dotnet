namespace Guardhouse.SDK.Models;

using System.Text.Json.Serialization;
using NodaTime;

/// <summary>
/// Represents the response from the token introspection endpoint.
/// </summary>
public record IntrospectionResponse
{
    /// <summary>
    /// Indicates whether the token is currently active.
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; init; }

    /// <summary>
    /// The scope(s) associated with the token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>
    /// The client ID that requested the token.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    /// <summary>
    /// The username or subject identifier.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// The type of the token (e.g., "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>
    /// The algorithm used to sign the token.
    /// </summary>
    [JsonPropertyName("alg")]
    public string? Algorithm { get; init; }

    /// <summary>
    /// The signature of the token.
    /// </summary>
    [JsonPropertyName("sig")]
    public string? Signature { get; init; }

    /// <summary>
    /// The Unix timestamp when the token expires.
    /// </summary>
    [JsonPropertyName("exp")]
    public long? Exp { get; init; }

    /// <summary>
    /// The Unix timestamp when the token was issued.
    /// </summary>
    [JsonPropertyName("iat")]
    public long? Iat { get; init; }

    /// <summary>
    /// The Unix timestamp before which the token must not be accepted.
    /// </summary>
    [JsonPropertyName("nbf")]
    public long? Nbf { get; init; }

    /// <summary>
    /// The subject identifier of the token.
    /// </summary>
    [JsonPropertyName("sub")]
    public string? Sub { get; init; }

    /// <summary>
    /// The audience(s) the token is intended for.
    /// </summary>
    [JsonPropertyName("aud")]
    public string? Aud { get; init; }

    /// <summary>
    /// The issuer of the token.
    /// </summary>
    [JsonPropertyName("iss")]
    public string? Iss { get; init; }

    /// <summary>
    /// The unique identifier of the token (JWT ID).
    /// </summary>
    [JsonPropertyName("jti")]
    public string? Jti { get; init; }

    /// <summary>
    /// Multiple roles assigned to the token subject, space-separated.
    /// </summary>
    [JsonPropertyName("roles")]
    public string? Roles { get; init; }

    /// <summary>
    /// Multiple roles assigned to the token subject as an array.
    /// </summary>
    [JsonPropertyName("role")]
    public string[]? Role { get; init; }

    /// <summary>
    /// Gets the expiration time as an Instant, or null if not available.
    /// </summary>
    public Instant? ExpiresAt => Exp.HasValue ? Instant.FromUnixTimeSeconds(Exp.Value) : null;

    /// <summary>
    /// Gets the issued-at time as an Instant, or null if not available.
    /// </summary>
    public Instant? IssuedAt => Iat.HasValue ? Instant.FromUnixTimeSeconds(Iat.Value) : null;

    /// <summary>
    /// Gets the not-before time as an Instant, or null if not available.
    /// </summary>
    public Instant? NotBefore => Nbf.HasValue ? Instant.FromUnixTimeSeconds(Nbf.Value) : null;
}
