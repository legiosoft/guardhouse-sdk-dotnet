using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Constants;

namespace Guardhouse.SDK.Services;

internal class GuardhouseIntrospectionJwtBearerEvents : JwtBearerEvents
{
    private readonly IOptions<GuardhouseResourceOptions> _options;
    private readonly IGuardhouseIntrospectionService _introspectionService;
    private readonly ILogger<GuardhouseIntrospectionJwtBearerEvents> _logger;

    public GuardhouseIntrospectionJwtBearerEvents(
        IOptions<GuardhouseResourceOptions> options,
        IGuardhouseIntrospectionService introspectionService,
        ILogger<GuardhouseIntrospectionJwtBearerEvents>? logger = null)
    {
        _options = options;
        _introspectionService = introspectionService;
        _logger = logger ?? NullLogger<GuardhouseIntrospectionJwtBearerEvents>.Instance;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var token = context.HttpContext.Request.Headers[GuardhouseConstants.Headers.Authorization].ToString();
        if (token.StartsWith(GuardhouseConstants.Headers.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring(GuardhouseConstants.Headers.BearerPrefix.Length);
        }

        try
        {
            var jwtToken = context.SecurityToken as JwtSecurityToken;
            if (jwtToken != null)
            {
                var typ = jwtToken.Header.Typ;
                if (!string.IsNullOrEmpty(typ) && !_options.Value.TokenTypes.Contains(typ))
                {
                    _logger.LogWarning("Token rejected: type '{TokenType}' is not allowed", typ);
                    context.Fail($"Token type '{typ}' is not allowed");
                    return;
                }

                var alg = jwtToken.Header.Alg;
                if (!_options.Value.ValidAlgorithms.Contains(alg))
                {
                    _logger.LogWarning("Token rejected: algorithm '{Algorithm}' is not allowed", alg);
                    context.Fail($"Algorithm '{alg}' is not allowed");
                    return;
                }
            }

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

            if (string.IsNullOrEmpty(introspectionResult.Signature))
            {
                _logger.LogWarning("Token rejected: introspection did not return signature");
                context.Fail("Token signature is missing");
                return;
            }

            AddIntrospectionClaims(context, introspectionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token introspection failed");
            context.Fail("Token introspection failed");
            return;
        }

        await base.TokenValidated(context);
    }

    private static void AddIntrospectionClaims(TokenValidatedContext context, IntrospectionResponse introspectionResult)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(introspectionResult.Sub))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.Subject, introspectionResult.Sub));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Username))
        {
            claims.Add(new Claim(ClaimTypes.Name, introspectionResult.Username));
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
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.Audience, introspectionResult.Aud));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Iss))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.Issuer, introspectionResult.Iss));
        }

        if (!string.IsNullOrEmpty(introspectionResult.Jti))
        {
            claims.Add(new Claim(GuardhouseConstants.JwtClaims.JwtId, introspectionResult.Jti));
        }

        if (claims.Count > 0)
        {
            var identity = new ClaimsIdentity(claims, context.Scheme.Name);
            context.Principal?.AddIdentity(identity);
        }
    }
}
