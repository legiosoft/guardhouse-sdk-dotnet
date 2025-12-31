using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Guardhouse.SDK.Models;

namespace Guardhouse.SDK.Services;

/// <summary>
/// Configurer for JWT bearer options using Guardhouse resource options
/// </summary>
internal class GuardhouseJwtBearerOptionsConfigurer : IConfigureOptions<JwtBearerOptions>, IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<GuardhouseResourceOptions> _resourceOptions;

    public GuardhouseJwtBearerOptionsConfigurer(IOptions<GuardhouseResourceOptions> resourceOptions)
    {
        _resourceOptions = resourceOptions;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var resourceOptions = _resourceOptions.Value;

        options.Authority = resourceOptions.Authority;
        options.Audience = resourceOptions.Audience;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = resourceOptions.ValidateIssuer,
            ValidIssuer = resourceOptions.Authority,
            ValidateAudience = resourceOptions.ValidateAudience && !string.IsNullOrEmpty(resourceOptions.Audience),
            ValidAudience = resourceOptions.Audience,
            ValidateLifetime = resourceOptions.ValidateLifetime,
            ClockSkew = resourceOptions.ClockSkew,
            RequireSignedTokens = true,
            RequireExpirationTime = true
        };

        // Configure HTTPS metadata requirement
        if (resourceOptions.RequireHttpsMetadata.HasValue)
        {
            options.RequireHttpsMetadata = resourceOptions.RequireHttpsMetadata.Value;
        }
        else
        {
            options.RequireHttpsMetadata = resourceOptions.Authority.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        }

        // Set custom events
        options.EventsType = typeof(GuardhouseJwtBearerEvents);
    }
}