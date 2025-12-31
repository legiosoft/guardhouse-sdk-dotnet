using System;
using System.ComponentModel.DataAnnotations;

namespace Guardhouse.SDK.Models;

/// <summary>
/// Configuration for Guardhouse SDK resource server functionality
/// </summary>
public class GuardhouseResourceOptions
{
    /// <summary>
    /// The Guardhouse server base URL
    /// </summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Audience for token validation
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Enable token introspection endpoint validation
    /// </summary>
    public bool EnableIntrospection { get; set; } = false;

    /// <summary>
    /// Client ID for introspection endpoint (if enabled)
    /// </summary>
    public string? IntrospectionClientId { get; set; }

    /// <summary>
    /// Client secret for introspection endpoint (if enabled)
    /// </summary>
    public string? IntrospectionClientSecret { get; set; }

    /// <summary>
    /// Token validation policy name (default: "Guardhouse")
    /// </summary>
    public string PolicyName { get; set; } = "Guardhouse";

    /// <summary>
    /// Clock skew for token validation (default: 5 minutes)
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validate issuer (default: true)
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Validate audience (default: true if Audience is specified)
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Validate token lifetime (default: true)
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Require HTTPS metadata (default: true for HTTPS authorities)
    /// </summary>
    public bool? RequireHttpsMetadata { get; set; }
}