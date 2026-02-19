namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
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
    private readonly IOptions<GuardhouseResourceOptions> _options = options;
    private readonly IGuardhouseIntrospectionService _introspectionService = introspectionService;
    private readonly ILogger<GuardhouseIntrospectionJwtBearerEvents> _logger = logger ?? NullLogger<GuardhouseIntrospectionJwtBearerEvents>.Instance;

    /// <summary>
    /// Validates a token by introspecting it with the identity server.
    /// Builds claims from the introspection response and sets them on the authentication context.
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (!TryGetAccessToken(context, out var token))
        {
            _logger.LogWarning("Token rejected: unable to extract raw token");
            context.Fail("Unable to extract token");
            return;
        }

        IntrospectionResponse introspectionResult;
        try
        {
            introspectionResult = await _introspectionService.IntrospectTokenAsync(
                token,
                context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection failed");
            context.Fail("Token introspection failed");
            return;
        }

        if (!introspectionResult.Active)
        {
            _logger.LogWarning("Token rejected: introspection returned active=false");
            context.Fail("Token is not active");
            return;
        }

        var tokenLooksLikeJwt = LooksLikeJwt(token);

        if (!IsTokenTypeAllowed(introspectionResult.TokenType))
        {
            _logger.LogWarning("Token rejected: introspection returned type '{TokenType}' which is not allowed", introspectionResult.TokenType);
            context.Fail($"Token type '{introspectionResult.TokenType}' is not allowed");
            return;
        }

        if (tokenLooksLikeJwt || !string.IsNullOrWhiteSpace(introspectionResult.Algorithm))
        {
            var validAlgorithms = GetValidAlgorithms(context.Options.TokenValidationParameters);
            if (!IsAlgorithmAllowed(introspectionResult.Algorithm, validAlgorithms))
            {
                _logger.LogWarning("Token rejected: introspection returned algorithm '{Algorithm}' which is not allowed", introspectionResult.Algorithm);
                context.Fail($"Algorithm '{introspectionResult.Algorithm}' is not allowed");
                return;
            }
        }

        var validationParameters = context.Options.TokenValidationParameters;
        if (validationParameters.ValidateIssuer && !IsIssuerAllowed(introspectionResult.Iss, validationParameters))
        {
            _logger.LogWarning("Token rejected: issuer '{Issuer}' is not allowed", introspectionResult.Iss);
            context.Fail("Token issuer is not allowed");
            return;
        }

        if (validationParameters.ValidateAudience && !IsAudienceAllowed(introspectionResult.Aud, validationParameters))
        {
            _logger.LogWarning("Token rejected: audience '{Audience}' is not allowed", introspectionResult.Aud);
            context.Fail("Token audience is not allowed");
            return;
        }

        if (validationParameters.ValidateLifetime)
        {
            var lifetimeError = ValidateLifetime(introspectionResult, validationParameters.ClockSkew, tokenLooksLikeJwt);
            if (lifetimeError != null)
            {
                _logger.LogWarning("Token rejected: {Reason}", lifetimeError);
                context.Fail(lifetimeError);
                return;
            }
        }

        var nameClaimType = string.IsNullOrWhiteSpace(validationParameters.NameClaimType)
            ? ClaimsIdentity.DefaultNameClaimType
            : validationParameters.NameClaimType;
        var roleClaimType = string.IsNullOrWhiteSpace(validationParameters.RoleClaimType)
            ? ClaimsIdentity.DefaultRoleClaimType
            : validationParameters.RoleClaimType;

        var claims = BuildClaimsFromIntrospection(introspectionResult, nameClaimType, roleClaimType);
        var identity = new ClaimsIdentity(claims, context.Scheme.Name, nameClaimType, roleClaimType);
        context.Principal = new ClaimsPrincipal(identity);

        await base.TokenValidated(context);
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

        return TryGetBearerToken(context.HttpContext.Request, out token);
    }

    private static bool TryGetBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;

        if (!request.Headers.TryGetValue(GuardhouseConstants.Headers.Authorization, out var authorization))
        {
            return false;
        }

        var headerValue = authorization.ToString();
        if (!headerValue.StartsWith(GuardhouseConstants.Headers.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = headerValue.Substring(GuardhouseConstants.Headers.BearerPrefix.Length).Trim();
        return !string.IsNullOrEmpty(token);
    }

    private bool IsTokenTypeAllowed(string? tokenType)
    {
        if (string.IsNullOrWhiteSpace(tokenType))
        {
            return true;
        }

        var allowedTokenTypes = _options.Value.IntrospectionTokenTypes;
        if (allowedTokenTypes == null || allowedTokenTypes.Length == 0)
        {
            return true;
        }

        return allowedTokenTypes.Any(type => string.Equals(type, tokenType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAlgorithmAllowed(string? algorithm, IEnumerable<string>? validAlgorithms)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            return false;
        }

        if (string.Equals(algorithm, GuardhouseConstants.Algorithms.None, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (validAlgorithms == null)
        {
            return false;
        }

        foreach (var validAlgorithm in validAlgorithms)
        {
            if (string.IsNullOrWhiteSpace(validAlgorithm))
            {
                continue;
            }

            if (string.Equals(validAlgorithm, algorithm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string>? GetValidAlgorithms(TokenValidationParameters validationParameters)
    {
        if (validationParameters.ValidAlgorithms != null && validationParameters.ValidAlgorithms.Any())
        {
            return validationParameters.ValidAlgorithms;
        }

        var configuredAlgorithms = _options.Value.ValidAlgorithms;
        return configuredAlgorithms != null && configuredAlgorithms.Length > 0
            ? configuredAlgorithms
            : validationParameters.ValidAlgorithms;
    }

    private static bool IsIssuerAllowed(string? issuer, TokenValidationParameters validationParameters)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return false;
        }

        var normalizedIssuer = NormalizeIssuer(issuer);

        if (!string.IsNullOrWhiteSpace(validationParameters.ValidIssuer) &&
            string.Equals(normalizedIssuer, NormalizeIssuer(validationParameters.ValidIssuer), StringComparison.Ordinal))
        {
            return true;
        }

        if (validationParameters.ValidIssuers == null)
        {
            return false;
        }

        foreach (var validIssuer in validationParameters.ValidIssuers)
        {
            if (string.IsNullOrWhiteSpace(validIssuer))
            {
                continue;
            }

            if (string.Equals(normalizedIssuer, NormalizeIssuer(validIssuer), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAudienceAllowed(string? audience, TokenValidationParameters validationParameters)
    {
        if (string.IsNullOrWhiteSpace(audience))
        {
            return false;
        }

        var validAudiences = GetValidAudiences(validationParameters);
        if (validAudiences.Count == 0)
        {
            return false;
        }

        foreach (var tokenAudience in SplitAudiences(audience))
        {
            if (validAudiences.Contains(tokenAudience))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> GetValidAudiences(TokenValidationParameters validationParameters)
    {
        var audiences = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(validationParameters.ValidAudience))
        {
            audiences.Add(validationParameters.ValidAudience);
        }

        if (validationParameters.ValidAudiences != null)
        {
            foreach (var validAudience in validationParameters.ValidAudiences)
            {
                if (!string.IsNullOrWhiteSpace(validAudience))
                {
                    audiences.Add(validAudience);
                }
            }
        }

        return audiences;
    }

    private static IEnumerable<string> SplitAudiences(string audiences)
    {
        return audiences.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(audience => audience.Trim())
            .Where(audience => !string.IsNullOrWhiteSpace(audience));
    }

    private static string? ValidateLifetime(IntrospectionResponse introspectionResult, TimeSpan clockSkew, bool requireExp)
    {
        var now = DateTimeOffset.UtcNow;

        if (introspectionResult.Nbf.HasValue)
        {
            try
            {
                var notBefore = DateTimeOffset.FromUnixTimeSeconds(introspectionResult.Nbf.Value);
                if (notBefore - clockSkew > now)
                {
                    return "Token is not yet valid";
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return "Token not-before value is invalid";
            }
        }

        if (!introspectionResult.Exp.HasValue)
        {
            return requireExp ? "Token missing exp claim" : null;
        }

        try
        {
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(introspectionResult.Exp.Value);
            if (expiresAt + clockSkew <= now)
            {
                return "Token has expired";
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Token expiration value is invalid";
        }

        return null;
    }

    private static bool LooksLikeJwt(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var firstDot = token.IndexOf('.');
        if (firstDot <= 0)
        {
            return false;
        }

        var secondDot = token.IndexOf('.', firstDot + 1);
        return secondDot > firstDot + 1 && secondDot < token.Length - 1;
    }

    private static string NormalizeIssuer(string issuer) => issuer.TrimEnd('/');

    private static List<Claim> BuildClaimsFromIntrospection(IntrospectionResponse introspectionResult,
        string nameClaimType, string roleClaimType)
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
            claims.Add(new Claim(nameClaimType, introspectionResult.Username));
        }

        if (introspectionResult.Role != null && introspectionResult.Role.Length > 0)
        {
            foreach (var role in introspectionResult.Role)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roles.Add(role);
                }
            }
        }

        if (!string.IsNullOrEmpty(introspectionResult.Roles))
        {
            var roleArray = introspectionResult.Roles.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var role in roleArray)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roles.Add(role);
                }
            }
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(roleClaimType, role));
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
            var audiences = SplitAudiences(introspectionResult.Aud);
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
