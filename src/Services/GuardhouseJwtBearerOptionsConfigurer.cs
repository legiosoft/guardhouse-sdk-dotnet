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

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

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
        Configure(options, resourceOptions.Authority, resourceOptions.Audience);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != null)
        {
            Configure(options, name);
        }
        else
        {
            Configure(options);
        }
    }
}
}