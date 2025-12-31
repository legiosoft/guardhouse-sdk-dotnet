using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Guardhouse.SDK.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Guardhouse.SDK.Services;

/// <summary>
/// Custom authentication events for Guardhouse JWT tokens with cancellation token support
/// </summary>
public class GuardhouseJwtBearerEvents : JwtBearerEvents
{
    private readonly IOptions<GuardhouseResourceOptions> _options;
    private readonly IGuardhouseTokenService? _tokenService;
    private readonly ILogger<GuardhouseJwtBearerEvents> _logger;

    public GuardhouseJwtBearerEvents(
        IOptions<GuardhouseResourceOptions> options,
        IGuardhouseTokenService? tokenService = null,
        ILogger<GuardhouseJwtBearerEvents>? logger = null)
    {
        _options = options;
        _tokenService = tokenService;
        _logger = logger ?? NullLogger<GuardhouseJwtBearerEvents>.Instance;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var options = _options.Value;

        // Handle cancellation request
        if (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request was cancelled during token validation");
            context.Fail("Request cancelled");
            return;
        }

        // Additional validation using introspection if enabled
        if (options.EnableIntrospection && _tokenService != null)
        {
            try
            {
                var token = context.HttpContext.Request.Headers["Authorization"].ToString();
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(7);
                    var introspectionResult = await _tokenService.IntrospectTokenAsync(
                        token, 
                        context.HttpContext.RequestAborted);
                    
                    if (!introspectionResult.Active)
                    {
                        _logger.LogWarning("Token introspection failed: token is not active");
                        context.Fail("Token is not active");
                        return;
                    }

                    // Add additional claims from introspection
                    await AddIntrospectionClaimsAsync(context, introspectionResult);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Token introspection was cancelled");
                context.Fail("Request cancelled");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token introspection failed");
                context.Fail("Token introspection failed");
                return;
            }
        }

        await base.TokenValidated(context);
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        // Handle cancellation during authentication failure
        if (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Authentication failed due to cancellation");
            context.Fail("Request cancelled");
            return Task.CompletedTask;
        }

        _logger.LogError(context.Exception, "Authentication failed");
        return base.AuthenticationFailed(context);
    }

    public override Task Challenge(JwtBearerChallengeContext context)
    {
        // Handle cancellation during challenge
        if (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Authentication challenge was cancelled");
            // For challenge, we just return without doing anything
            return Task.CompletedTask;
        }

        _logger.LogDebug("Authentication challenge issued");
        return base.Challenge(context);
    }

    private static async Task AddIntrospectionClaimsAsync(TokenValidatedContext context, IntrospectionResponse introspectionResult)
    {
        var claims = new List<Claim>();

        if (introspectionResult.Sub != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, introspectionResult.Sub));
        }

        if (introspectionResult.Username != null)
        {
            claims.Add(new Claim(ClaimTypes.Name, introspectionResult.Username));
        }

        if (introspectionResult.Scope != null)
        {
            var scopes = introspectionResult.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }
        }

        if (introspectionResult.ClientId != null)
        {
            claims.Add(new Claim("client_id", introspectionResult.ClientId));
        }

        if (introspectionResult.Aud != null)
        {
            claims.Add(new Claim("audience", introspectionResult.Aud));
        }

        if (introspectionResult.Iss != null)
        {
            claims.Add(new Claim("issuer", introspectionResult.Iss));
        }

        if (introspectionResult.Jti != null)
        {
            claims.Add(new Claim("jwt_id", introspectionResult.Jti));
        }

        if (claims.Count > 0)
        {
            var identity = new ClaimsIdentity(claims, context.Scheme.Name);
            context.Principal?.AddIdentity(identity);
        }
    }
}