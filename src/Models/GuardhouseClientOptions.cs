namespace Guardhouse.SDK.Models;

using System.ComponentModel.DataAnnotations;
using Constants;

public class GuardhouseClientOptions
{
    [Required]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    public string Scope { get; set; } = GuardhouseConstants.Defaults.DefaultScope;

    public bool EnableTokenCaching { get; set; } = true;

    public int CacheExpirationBufferSeconds { get; set; } = GuardhouseConstants.Defaults.CacheExpirationBufferSeconds;

    public bool EnableTokenRefresh { get; set; } = true;

    public int RequestTimeoutSeconds { get; set; } = GuardhouseConstants.Defaults.RequestTimeoutSeconds;

    public int MaxRetryAttempts { get; set; } = GuardhouseConstants.Defaults.MaxRetryAttempts;

    public bool EnableHttpResilience { get; set; } = true;
}
