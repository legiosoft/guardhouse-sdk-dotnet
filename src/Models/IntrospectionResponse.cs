namespace Guardhouse.SDK.Models;

using System.Text.Json.Serialization;
using NodaTime;

/// <summary>
/// Represents the response from the token introspection endpoint.
/// </summary>
public class IntrospectionResponse
{
    /// <summary>
    /// Indicates whether the token is currently active.
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>
    /// The scope(s) associated with the token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// The client ID that requested the token.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The username or subject identifier.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// The type of the token (e.g., "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    /// <summary>
    /// The algorithm used to sign the token.
    /// </summary>
    [JsonPropertyName("alg")]
    public string? Algorithm { get; set; }

    /// <summary>
    /// The signature of the token.
    /// </summary>
    [JsonPropertyName("sig")]
    public string? Signature { get; set; }

    /// <summary>
    /// The Unix timestamp when the token expires.
    /// </summary>
    [JsonPropertyName("exp")]
    public long? Exp { get; set; }

    /// <summary>
    /// The Unix timestamp when the token was issued.
    /// </summary>
    [JsonPropertyName("iat")]
    public long? Iat { get; set; }

    /// <summary>
    /// The Unix timestamp before which the token must not be accepted.
    /// </summary>
    [JsonPropertyName("nbf")]
    public long? Nbf { get; set; }

    /// <summary>
    /// The subject identifier of the token.
    /// </summary>
    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    /// <summary>
    /// The audience(s) the token is intended for.
    /// </summary>
    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    /// <summary>
    /// The issuer of the token.
    /// </summary>
    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    /// <summary>
    /// The unique identifier of the token (JWT ID).
    /// </summary>
    [JsonPropertyName("jti")]
    public string? Jti { get; set; }

    /// <summary>
    /// The role assigned to the token subject.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Multiple roles assigned to the token subject, space-separated.
    /// </summary>
    [JsonPropertyName("roles")]
    public string? Roles { get; set; }

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
