namespace Guardhouse.SDK.Services;

using System.Collections.Generic;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Constants;
using Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// JWT bearer events handler that performs token introspection validation.
/// This replaces the default JWT signature validation with introspection-based validation.
/// </summary>
public class GuardhouseIntrospectionJwtBearerEvents(
    IOptions<GuardhouseResourceOptions> options,
    IGuardhouseIntrospectionService introspectionService,
    ILogger<GuardhouseIntrospectionJwtBearerEvents>? logger = null) : JwtBearerEvents
{
    private readonly IOptions<GuardhouseResourceOptions> _options = options;
    private readonly IGuardhouseIntrospectionService _introspectionService = introspectionService;
    private readonly ILogger<GuardhouseIntrospectionJwtBearerEvents> _logger = logger ?? NullLogger<GuardhouseIntrospectionJwtBearerEvents>.Instance;

    /// <summary>
    /// Validates a token by introspecting it with the identity server.
    /// Builds claims from the introspection response and sets them on the authentication context.
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var token = context.SecurityToken is JwtSecurityToken jwtToken ? jwtToken.RawData : null;
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Token rejected: unable to extract raw token");
            context.Fail("Unable to extract token");
            return;
        }

        try
        {
            var introspectionResult = await _introspectionService.IntrospectTokenAsync(token, context.HttpContext.RequestAborted);

            if (!introspectionResult.Active)
            {
                _logger.LogWarning("Token rejected: introspection returned active=false");
                context.Fail("Token is not active");
                return;
            }

            if (!string.IsNullOrEmpty(introspectionResult.TokenType) &&
                !_options.Value.TokenTypes.Contains(introspectionResult.TokenType))
            {
                _logger.LogWarning("Token rejected: introspection returned type '{TokenType}' which is not allowed", introspectionResult.TokenType);
                context.Fail($"Token type '{introspectionResult.TokenType}' is not allowed");
                return;
            }

            if (!string.IsNullOrEmpty(introspectionResult.Algorithm) &&
                !_options.Value.ValidAlgorithms.Contains(introspectionResult.Algorithm))
            {
                _logger.LogWarning("Token rejected: introspection returned algorithm '{Algorithm}' which is not allowed", introspectionResult.Algorithm);
                context.Fail($"Algorithm '{introspectionResult.Algorithm}' is not allowed");
                return;
            }

            var claims = BuildClaimsFromIntrospection(introspectionResult);
            var identity = new ClaimsIdentity(claims, context.Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
            context.Principal = new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection failed");
            context.Fail("Token introspection failed");
            return;
        }

        await base.TokenValidated(context);
    }

    private static List<Claim> BuildClaimsFromIntrospection(IntrospectionResponse introspectionResult)
    {
        var claims = new List<Claim>();
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(introspectionResult.Sub))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, introspectionResult.Sub));
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.Subject, introspectionResult.Sub));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Username))
        {
            claims.Add(new Claim(ClaimTypes.Name, introspectionResult.Username));
        }

        if (introspectionResult.Role != null && introspectionResult.Role.Length > 0)
        {
            foreach (var role in introspectionResult.Role)
            {
                roles.Add(role);
            }
        }

        if (!string.IsNullOrEmpty(introspectionResult.Roles))
        {
            var roleArray = introspectionResult.Roles.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var role in roleArray)
            {
                roles.Add(role);
            }
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Scope))
        {
            var scopes = introspectionResult.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var scope in scopes)
            {
                claims.Add(new Claim(GuardhouseConstants.JwtClaims.Scope, scope));
            }
        }

        if (!string.IsNullOrEmpty(introspectionResult.ClientId))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.ClientId, introspectionResult.ClientId));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Aud))
        {
            var audiences = introspectionResult.Aud.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var audience in audiences)
            {
                claims.Add(new Claim(GuardhouseConstants.JwtClaims.Audience, audience));
            }
        }

        if (!string.IsNullOrEmpty(introspectionResult.Iss))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.Issuer, introspectionResult.Iss));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Jti))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.JwtId, introspectionResult.Jti));
        }

        return claims;
    }
}
