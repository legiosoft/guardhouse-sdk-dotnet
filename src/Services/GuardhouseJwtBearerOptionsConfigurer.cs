using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Guardhouse.SDK.Models;

namespace Guardhouse.SDK.Services;

/// <summary>
/// Configurer for JWT bearer options using Guardhouse resource options
/// </summary>
internal class GuardhouseJwtBearerOptionsConfigurer : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<GuardhouseResourceOptions> _resourceOptions;

    public GuardhouseJwtBearerOptionsConfigurer(IOptions<GuardhouseResourceOptions> resourceOptions)
    {
        _resourceOptions = resourceOptions;
    }

    public void Configure(JwtBearerOptions options)
    {
        var resourceOptions = _resourceOptions.Value;
        if (resourceOptions.Authority != null && resourceOptions.Audience != null)
        {
            Configure(options, resourceOptions.Authority, resourceOptions.Audience);
        }
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        var resourceOptions = _resourceOptions.Value;
        if (name != null)
        {
            Configure(options, name, resourceOptions.Audience ?? string.Empty);
        }
        else
        {
            Configure(options);
        }
    }

    private void Configure(JwtBearerOptions options, string authority, string audience)
    {
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    }
}