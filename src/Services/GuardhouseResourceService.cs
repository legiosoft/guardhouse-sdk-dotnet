namespace Guardhouse.SDK.Services;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Service for managing Guardhouse resource server operations.
/// </summary>
public class GuardhouseResourceService(
    IGuardhouseIntrospectionService introspectionService,
    IAuthenticationSchemeProvider schemeProvider,
    IOptions<GuardhouseResourceOptions> resourceOptions,
    IOptionsMonitor<JwtBearerOptions> jwtBearerOptions,
    ILogger<GuardhouseResourceService>? logger = null)
    : IGuardhouseResourceService
{
    private readonly ILogger<GuardhouseResourceService> _logger = logger ?? NullLogger<GuardhouseResourceService>.Instance;

    /// <summary>
    /// Validates a token and returns the claims principal if valid.
    /// Uses the configured validation mode (JWT signature or introspection).
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The claims principal if the token is valid, or null.</returns>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var options = resourceOptions.Value;
        var schemeName = options.PolicyName;
        if (string.IsNullOrWhiteSpace(schemeName))
        {
            var scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
            schemeName = scheme?.Name ?? GuardhouseConstants.Authentication.DefaultScheme;
        }

        var jwtOptions = jwtBearerOptions.Get(schemeName);

        if (options.EnableIntrospection)
        {
            IntrospectionResponse introspectionResult;
            try
            {
                introspectionResult = await introspectionService.IntrospectTokenAsync(token, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed due to an introspection error");
                return null;
            }

            var (identity, failureReason) = GuardhouseIntrospectionLogic.BuildIdentityFromIntrospection(
                token,
                introspectionResult,
                jwtOptions.TokenValidationParameters,
                options,
                schemeName);

            if (identity == null)
            {
                _logger.LogWarning("Token rejected: {Reason}", failureReason);
                return null;
            }

            if (GuardhouseIntrospectionLogic.LooksLikeJwt(token))
            {
                var jwtPrincipal = await TryValidateJwtAsync(token, jwtOptions, cancellationToken);
                if (jwtPrincipal == null)
                {
                    _logger.LogWarning("Token rejected: introspection succeeded but local JWT validation failed");
                    return null;
                }

                if (jwtPrincipal.Identity is ClaimsIdentity jwtIdentity)
                {
                    GuardhouseIntrospectionLogic.MergeClaims(jwtIdentity, identity.Claims);
                }

                return jwtPrincipal;
            }

            return new ClaimsPrincipal(identity);
        }

        return await TryValidateJwtAsync(token, jwtOptions, cancellationToken);
    }

    private async Task<ClaimsPrincipal?> TryValidateJwtAsync(
        string token,
        JwtBearerOptions jwtOptions,
        CancellationToken cancellationToken)
    {
        if (jwtOptions.TokenValidationParameters.IssuerSigningKeyResolver?.Target is GuardhouseJwksSigningKeyResolver resolver)
        {
            await resolver.WarmupAsync(cancellationToken);
        }

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = jwtOptions.MapInboundClaims
        };

        if (!handler.CanReadToken(token))
        {
            return null;
        }

        var validationParameters = jwtOptions.TokenValidationParameters.Clone();
        if (jwtOptions.ConfigurationManager != null)
        {
            var configuration = await jwtOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);
            if (configuration != null)
            {
                if (validationParameters.ValidIssuers == null && string.IsNullOrWhiteSpace(validationParameters.ValidIssuer))
                {
                    validationParameters.ValidIssuers = [configuration.Issuer];
                }

                validationParameters.IssuerSigningKeys = configuration.SigningKeys;
            }
        }

        try
        {
            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenSignatureKeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Token signature key not found");
            jwtOptions.ConfigurationManager?.RequestRefresh();
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }


    /// <summary>
    /// Introspects a token to determine if it is active.
    /// </summary>
    /// <param name="token">The token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response.</returns>
    public Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return introspectionService.IntrospectTokenAsync(token, cancellationToken);
    }

    /// <summary>
    /// Gets the default authentication scheme configured for the application.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The default authentication scheme, or null if not configured.</returns>
    public async Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default)
    {
        var scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
        return scheme;
    }
}
