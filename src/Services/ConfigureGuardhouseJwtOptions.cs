namespace Guardhouse.SDK.Services;

using System;
using System.Threading.Tasks;
using Constants;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

internal class ConfigureGuardhouseJwtOptions(IOptions<GuardhouseResourceOptions> resourceOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string GuardhouseSchemeName = "Guardhouse";

    public void Configure(JwtBearerOptions options)
    {
        Configure(GuardhouseSchemeName, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != GuardhouseSchemeName)
        {
            return;
        }

        var opts = resourceOptions.Value;

        options.Authority = opts.Authority;
        options.Audience = opts.Audience;
        options.RequireHttpsMetadata = opts.RequireHttpsMetadata ?? opts.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (opts.EnableIntrospection)
        {
            options.EventsType = typeof(GuardhouseIntrospectionJwtBearerEvents);
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = opts.ValidateIssuer,
                ValidIssuer = opts.Authority,

                ValidateAudience = opts.ValidateAudience,
                ValidAudience = opts.Audience,

                ValidateLifetime = opts.ValidateLifetime,
                ClockSkew = TimeSpan.FromMinutes(GuardhouseConstants.Defaults.ClockSkewMinutes),

                ValidateIssuerSigningKey = opts.ValidateIssuerSigningKey,

                ValidAlgorithms = opts.ValidAlgorithms,

                ValidTypes = opts.TokenTypes,
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers["Token-Expired"] = "true";
                    }

                    return Task.CompletedTask;
                }
            };

            options.RefreshOnIssuerKeyNotFound = true;

            options.AutomaticRefreshInterval = TimeSpan.FromMinutes(opts.JwksRefreshIntervalMinutes);
            options.BackchannelTimeout = TimeSpan.FromSeconds(GuardhouseConstants.Defaults.RequestTimeoutSeconds);
        }
    }
}
