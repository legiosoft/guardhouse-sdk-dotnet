namespace Guardhouse.SDK.Models;

using System.ComponentModel.DataAnnotations;
using Constants;
using Validation;

public class GuardhouseResourceOptions
{
    [Required(ErrorMessage = "Authority is required")]
    public string Authority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Audience is required")]
    public string Audience { get; set; } = string.Empty;

    public TokenValidationMode ValidationMode { get; set; } = TokenValidationMode.JwtSignature;

    [RequiredIfIntrospection(ErrorMessage = "IntrospectionClientId is required when ValidationMode is Introspection")]
    public string? IntrospectionClientId { get; set; }

    [RequiredIfIntrospection(ErrorMessage = "IntrospectionClientSecret is required when ValidationMode is Introspection")]
    public string? IntrospectionClientSecret { get; set; }

    public bool EnableIntrospection => ValidationMode == TokenValidationMode.Introspection;

    public string PolicyName { get; set; } = "Guardhouse";

    public bool ValidateIssuer { get; set; } = GuardhouseConstants.Validation.ValidateIssuer;

    public bool ValidateAudience { get; set; } = GuardhouseConstants.Validation.ValidateAudience;

    public bool ValidateLifetime { get; set; } = GuardhouseConstants.Validation.ValidateLifetime;

    public bool ValidateIssuerSigningKey { get; set; } = GuardhouseConstants.Validation.ValidateIssuerSigningKey;

    public bool? RequireHttpsMetadata { get; set; }

    public int JwksCacheDurationHours { get; set; } = GuardhouseConstants.Defaults.JwksCacheDurationHours;

    public int JwksRefreshIntervalMinutes { get; set; } = GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes;

    public int IntrospectionCacheTtlSeconds { get; set; } = GuardhouseConstants.Defaults.IntrospectionCacheTtlSeconds;

    public string[] ValidAlgorithms { get; set; } = [GuardhouseConstants.Algorithms.RS256];

    public string[] TokenTypes { get; set; } = [GuardhouseConstants.TokenTypes.Jwt];
}
