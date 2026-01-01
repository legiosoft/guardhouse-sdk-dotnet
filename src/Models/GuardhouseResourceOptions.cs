using System.ComponentModel.DataAnnotations;

namespace Guardhouse.SDK.Models;

public class GuardhouseResourceOptions
{
    [Required]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = "my_resource_api";

    public TokenValidationMode ValidationMode { get; set; } = TokenValidationMode.JwtSignature;

    public string? IntrospectionClientId { get; set; }

    public string? IntrospectionClientSecret { get; set; }

    public bool EnableIntrospection => ValidationMode == TokenValidationMode.Introspection;

    public string PolicyName { get; set; } = "Guardhouse";

    public bool ValidateIssuer { get; set; } = Constants.GuardhouseConstants.Validation.ValidateIssuer;

    public bool ValidateAudience { get; set; } = Constants.GuardhouseConstants.Validation.ValidateAudience;

    public bool ValidateLifetime { get; set; } = Constants.GuardhouseConstants.Validation.ValidateLifetime;

    public bool ValidateIssuerSigningKey { get; set; } = Constants.GuardhouseConstants.Validation.ValidateIssuerSigningKey;

    public bool? RequireHttpsMetadata { get; set; }

    public int JwksCacheDurationHours { get; set; } = Constants.GuardhouseConstants.Defaults.JwksCacheDurationHours;

    public int JwksRefreshIntervalMinutes { get; set; } = Constants.GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes;

    public string[] ValidAlgorithms { get; set; } = { Constants.GuardhouseConstants.Algorithms.RS256 };

    public string[] TokenTypes { get; set; } = { Constants.GuardhouseConstants.TokenTypes.Jwt };
}
