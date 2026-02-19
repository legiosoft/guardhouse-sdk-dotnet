// ReSharper disable MergeIntoPattern
namespace Guardhouse.SDK.Services;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models;

/// <summary>
/// JWT bearer events handler that performs token introspection validation.
/// This replaces the default JWT signature validation with introspection-based validation.
/// </summary>
public class GuardhouseIntrospectionJwtBearerEvents(
    IOptions<GuardhouseResourceOptions> options,
    IGuardhouseIntrospectionService introspectionService,
    ILogger<GuardhouseIntrospectionJwtBearerEvents>? logger = null) : JwtBearerEvents
{
    private readonly ILogger<GuardhouseIntrospectionJwtBearerEvents> _logger = logger ?? NullLogger<GuardhouseIntrospectionJwtBearerEvents>.Instance;

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.SecurityToken is GuardhouseOpaqueSecurityToken &&
            context.Principal?.Identity is ClaimsIdentity existingIdentity &&
            existingIdentity.IsAuthenticated &&
            string.Equals(existingIdentity.AuthenticationType, GuardhouseConstants.Authentication.DefaultScheme, StringComparison.Ordinal))
        {
            await base.TokenValidated(context);
            return;
        }

        if (!TryGetAccessToken(context, out var token))
        {
            _logger.LogWarning("Token rejected: unable to extract raw token");
            context.Fail("Unable to extract token");
            return;
        }

        var (identity, failureReason) = await BuildIdentityFromIntrospectionAsync(
            token,
            context.Options.TokenValidationParameters,
            context.Scheme.Name,
            context.HttpContext.RequestAborted);

        if (identity == null)
        {
            context.Fail(failureReason ?? "Token validation failed");
            return;
        }

        if (context.SecurityToken is JwtSecurityToken && context.Principal?.Identity is ClaimsIdentity jwtIdentity)
        {
            GuardhouseIntrospectionLogic.MergeClaims(jwtIdentity, identity.Claims);
        }
        else
        {
            context.Principal = new ClaimsPrincipal(identity);
        }

        await base.TokenValidated(context);
    }

    private async Task<(ClaimsIdentity? Identity, string? FailureReason)> BuildIdentityFromIntrospectionAsync(
        string token,
        TokenValidationParameters validationParameters,
        string authenticationType,
        CancellationToken cancellationToken)
    {
        IntrospectionResponse introspectionResult;
        try
        {
            introspectionResult = await introspectionService.IntrospectTokenAsync(token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection failed");
            return (null, "Token introspection failed");
        }

        var result = GuardhouseIntrospectionLogic.BuildIdentityFromIntrospection(
            token,
            introspectionResult,
            validationParameters,
            options.Value,
            authenticationType);

        if (result.Identity == null)
        {
            _logger.LogWarning("Token rejected: {Reason}", result.FailureReason);
        }

        return result;
    }

    private bool TryGetAccessToken(TokenValidatedContext context, out string token)
    {
        token = string.Empty;
        if (context.SecurityToken is JwtSecurityToken jwtToken && !string.IsNullOrEmpty(jwtToken.RawData))
        {
            token = jwtToken.RawData;
            return true;
        }

        if (context.SecurityToken is GuardhouseOpaqueSecurityToken opaqueToken && !string.IsNullOrWhiteSpace(opaqueToken.Token))
        {
            token = opaqueToken.Token;
            return true;
        }

        return false;
    }

}
