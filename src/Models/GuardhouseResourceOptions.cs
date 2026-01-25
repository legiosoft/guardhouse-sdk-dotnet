namespace Guardhouse.SDK.Models;

using Constants;
using System.ComponentModel.DataAnnotations;
using Validation;

/// <summary>
/// Configuration options for the Guardhouse resource server used to validate incoming JWT tokens.
/// </summary>
public class GuardhouseResourceOptions
{
    [Required(ErrorMessage = "Authority is required")]
    public string Authority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Audience is required")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Determines how tokens are validated: by JWT signature or via introspection endpoint.
    /// </summary>
    public TokenValidationMode ValidationMode { get; set; } = TokenValidationMode.JwtSignature;

    [RequiredIfIntrospection(ErrorMessage = "IntrospectionClientId is required when ValidationMode is Introspection")]
    public string? IntrospectionClientId { get; set; }

    [RequiredIfIntrospection(ErrorMessage = "IntrospectionClientSecret is required when ValidationMode is Introspection")]
    public string? IntrospectionClientSecret { get; set; }

    public bool EnableIntrospection => ValidationMode == TokenValidationMode.Introspection;

    /// <summary>
    /// The policy name for the JWT bearer authentication scheme (default: "Guardhouse").
    /// </summary>
    public string PolicyName { get; set; } = GuardhouseConstants.Authentication.DefaultScheme;

    /// <summary>
    /// Whether to validate the token issuer (default: true).
    /// </summary>
    public bool ValidateIssuer { get; set; } = GuardhouseConstants.Validation.ValidateIssuer;

    /// <summary>
    /// Whether to validate the token audience (default: true).
    /// </summary>
    public bool ValidateAudience { get; set; } = GuardhouseConstants.Validation.ValidateAudience;

    /// <summary>
    /// Whether to validate the token lifetime (default: true).
    /// </summary>
    public bool ValidateLifetime { get; set; } = GuardhouseConstants.Validation.ValidateLifetime;

    /// <summary>
    /// Whether to validate the issuer signing key (default: true).
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = GuardhouseConstants.Validation.ValidateIssuerSigningKey;

    /// <summary>
    /// Whether HTTPS metadata is required. When null, uses the authority URL scheme (default: null).
    /// </summary>
    public bool? RequireHttpsMetadata { get; set; }

    /// <summary>
    /// How long to cache the JWKS (JSON Web Key Set) in hours (default: 24).
    /// </summary>
    public int JwksCacheDurationHours { get; set; } = GuardhouseConstants.Defaults.JwksCacheDurationHours;

    /// <summary>
    /// How often to refresh the JWKS cache in minutes (default: 5).
    /// </summary>
    public int JwksRefreshIntervalMinutes { get; set; } = GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes;

    /// <summary>
    /// The time-to-live for introspection cache entries in seconds (default: 5).
    /// This micro-cache strategy handles burst traffic while maintaining near-real-time revocation security.
    /// </summary>
    public int IntrospectionCacheTtlSeconds { get; set; } = GuardhouseConstants.Defaults.IntrospectionCacheTtlSeconds;

    /// <summary>
    /// How to send client credentials to the introspection endpoint (default: BasicAuth).
    /// Use FormData if your identity server does not accept Basic Authentication.
    /// </summary>
    public IntrospectionCredentialTransmission IntrospectionCredentialTransmission { get; set; } = IntrospectionCredentialTransmission.BasicAuth;

    /// <summary>
    /// The list of valid signing algorithms for tokens (default: ["RS256"]).
    /// </summary>
    public string[] ValidAlgorithms { get; set; } = [GuardhouseConstants.Algorithms.RS256];

    /// <summary>
    /// The list of valid token types (default: ["JWT"]).
    /// </summary>
    public string[] TokenTypes { get; set; } = [GuardhouseConstants.TokenTypes.Jwt];

    /// <summary>
    /// Enables debug logging including console output and debug-level logger messages (default: false).
    /// </summary>
    public bool EnableDebug { get; set; }
}
