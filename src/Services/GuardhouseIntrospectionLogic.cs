namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Constants;
using Microsoft.IdentityModel.Tokens;
using Models;

internal static class GuardhouseIntrospectionLogic
{
    internal static (ClaimsIdentity? Identity, string? FailureReason) BuildIdentityFromIntrospection(
        string token,
        IntrospectionResponse introspectionResult,
        TokenValidationParameters validationParameters,
        GuardhouseResourceOptions resourceOptions,
        string authenticationType)
    {
        if (!introspectionResult.Active)
        {
            return (null, "Token is not active");
        }

        if (!IsTokenTypeAllowed(introspectionResult.TokenType, resourceOptions.IntrospectionTokenTypes))
        {
            return (null, $"Token type '{introspectionResult.TokenType}' is not allowed");
        }

        var tokenLooksLikeJwt = LooksLikeJwt(token);

        if (tokenLooksLikeJwt || !string.IsNullOrWhiteSpace(introspectionResult.Algorithm))
        {
            var validAlgorithms = GetValidAlgorithms(validationParameters, resourceOptions.ValidAlgorithms);
            if (!IsAlgorithmAllowed(introspectionResult.Algorithm, validAlgorithms))
            {
                return (null, $"Algorithm '{introspectionResult.Algorithm}' is not allowed");
            }
        }

        if (validationParameters.ValidateIssuer && !IsIssuerAllowed(introspectionResult.Iss, validationParameters))
        {
            return (null, "Token issuer is not allowed");
        }

        if (validationParameters.ValidateAudience && !IsAudienceAllowed(introspectionResult.Aud, validationParameters))
        {
            return (null, "Token audience is not allowed");
        }

        if (validationParameters.ValidateLifetime)
        {
            var lifetimeError = ValidateLifetime(
                introspectionResult,
                validationParameters.ClockSkew,
                validationParameters.RequireExpirationTime);
            if (lifetimeError != null)
            {
                return (null, lifetimeError);
            }
        }

        var nameClaimType = string.IsNullOrWhiteSpace(validationParameters.NameClaimType)
            ? ClaimsIdentity.DefaultNameClaimType
            : validationParameters.NameClaimType;
        var roleClaimType = string.IsNullOrWhiteSpace(validationParameters.RoleClaimType)
            ? ClaimsIdentity.DefaultRoleClaimType
            : validationParameters.RoleClaimType;

        var claims = BuildClaimsFromIntrospection(introspectionResult, nameClaimType, roleClaimType);
        var identity = new ClaimsIdentity(claims, authenticationType, nameClaimType, roleClaimType);
        return (identity, null);
    }

    internal static void MergeClaims(ClaimsIdentity target, IEnumerable<Claim> additionalClaims)
    {
        var existingClaims = new HashSet<Claim>(target.Claims, ClaimTypeValueComparer.Instance);
        foreach (var claim in additionalClaims)
        {
            if (existingClaims.Add(claim))
            {
                target.AddClaim(claim);
            }
        }
    }

    private sealed class ClaimTypeValueComparer : IEqualityComparer<Claim>
    {
        public static ClaimTypeValueComparer Instance { get; } = new();

        public bool Equals(Claim? x, Claim? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Type, y.Type, StringComparison.Ordinal)
                   && string.Equals(x.Value, y.Value, StringComparison.Ordinal);
        }

        public int GetHashCode(Claim obj)
        {
            return HashCode.Combine(obj.Type, obj.Value);
        }
    }

    private static bool IsTokenTypeAllowed(string? tokenType, string[]? allowedTokenTypes)
    {
        if (string.IsNullOrWhiteSpace(tokenType))
        {
            return true;
        }

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

    private static IEnumerable<string>? GetValidAlgorithms(
        TokenValidationParameters validationParameters,
        string[] configuredAlgorithms)
    {
        if (validationParameters.ValidAlgorithms != null && validationParameters.ValidAlgorithms.Any())
        {
            return validationParameters.ValidAlgorithms;
        }

        return configuredAlgorithms.Length > 0
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
        return audiences.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(audience => audience.Trim())
            .Where(audience => !string.IsNullOrWhiteSpace(audience));
    }

    private static string? ValidateLifetime(
        IntrospectionResponse introspectionResult,
        TimeSpan clockSkew,
        bool requireExp)
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

    internal static bool LooksLikeJwt(string token)
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

    private static string NormalizeIssuer(string issuer)
    {
        return issuer.TrimEnd('/');
    }

    private static List<Claim> BuildClaimsFromIntrospection(
        IntrospectionResponse introspectionResult,
        string nameClaimType,
        string roleClaimType)
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

        if (introspectionResult.Role is { Length: > 0 })
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
