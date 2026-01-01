using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Constants;

namespace Guardhouse.SDK.Services;

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
        var resourceOptions = _resourceOptions.Value;
        Configure(options, resourceOptions.Authority, resourceOptions.Audience);
    }

    private void Configure(JwtBearerOptions options, string authority, string audience)
    {
        var resourceOptions = _resourceOptions.Value;
        
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = resourceOptions.RequireHttpsMetadata ?? authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (resourceOptions.EnableIntrospection)
        {
            options.EventsType = typeof(GuardhouseIntrospectionJwtBearerEvents);
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = resourceOptions.ValidateIssuer,
                ValidIssuer = authority,

                ValidateAudience = resourceOptions.ValidateAudience,
                ValidAudience = audience,

                ValidateLifetime = resourceOptions.ValidateLifetime,
                ClockSkew = TimeSpan.FromMinutes(GuardhouseConstants.Defaults.ClockSkewMinutes),

                ValidateIssuerSigningKey = resourceOptions.ValidateIssuerSigningKey,

                ValidAlgorithms = resourceOptions.ValidAlgorithms,

                ValidTypes = resourceOptions.TokenTypes
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var jwtToken = context.SecurityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                    if (jwtToken != null)
                    {
                        if (!string.IsNullOrEmpty(jwtToken.Header.Typ) && 
                            !resourceOptions.TokenTypes.Contains(jwtToken.Header.Typ))
                        {
                            context.Fail($"Token type '{jwtToken.Header.Typ}' is not allowed");
                            return;
                        }

                        if (!resourceOptions.ValidAlgorithms.Contains(jwtToken.Header.Alg))
                        {
                            context.Fail($"Algorithm '{jwtToken.Header.Alg}' is not allowed");
                            return;
                        }
                    }
                    await Task.CompletedTask;
                },

                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers["Token-Expired"] = "true";
                    }
                    return Task.CompletedTask;
                }
            };

            options.RefreshOnIssuerKeyNotFound = true;
            options.AutomaticRefreshInterval = TimeSpan.FromMinutes(resourceOptions.JwksRefreshIntervalMinutes);
            options.BackchannelTimeout = TimeSpan.FromSeconds(GuardhouseConstants.Defaults.RequestTimeoutSeconds);
        }
    }
}
