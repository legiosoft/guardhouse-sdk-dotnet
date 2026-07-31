using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;

namespace ExampleResource.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenInfoController : ControllerBase
{
    private readonly IGuardhouseResourceService _resourceService;

    public TokenInfoController(IGuardhouseResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    [HttpGet("info")]
    [Authorize]
    public ActionResult GetTokenInfo()
    {
        var accessToken = Request.Headers.Authorization.ToString();
        var audiences = ParseAudiences(
            User.FindAll(GuardhouseConstants.JwtClaims.Audience).Select(claim => claim.Value));
        
        var scopes = User.FindAll(GuardhouseConstants.JwtClaims.Scope)
            .Concat(User.FindAll("scp"))
            .SelectMany(claim => ParseDelimitedValues(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        var roles = User.FindAll(ClaimTypes.Role)
            .Concat(User.FindAll("role"))
            .Concat(User.FindAll("roles"))
            .SelectMany(claim => ParseDelimitedValues(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        var systemClaims = User.FindAll(AuthorizationConsts.ClaimTypes.System)
            .SelectMany(claim => ParseDelimitedValues(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        var businessClaims = User.FindAll(AuthorizationConsts.ClaimTypes.Business)
            .SelectMany(claim => ParseDelimitedValues(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        var parsedClaims = User.Claims
            .GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Type = group.Key,
                Values = group.Select(claim => claim.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        return Ok(new
        {
            Authenticated = User.Identity?.IsAuthenticated ?? false,
            UserName = User.Identity?.Name,
            User.Identity?.AuthenticationType,
            Subject = User.FindFirst("sub")?.Value,
            Issuer = User.FindFirst("iss")?.Value,
            Audience = audiences,
            ExpiresAt = User.FindFirst("exp")?.Value,
            IssuedAt = User.FindFirst("iat")?.Value,
            Scopes = scopes,
            Roles = roles,
            SystemClaims = systemClaims,
            BusinessClaims = businessClaims,
            TokenType = User.FindFirst("typ")?.Value,
            JwtId = User.FindFirst("jti")?.Value,
            ParsedClaims = parsedClaims,
            AccessTokenPreview = accessToken?.StartsWith("Bearer ") == true
                ? accessToken.Substring(7).GetPreview(20)
                : "N/A"
        });
    }

    [HttpGet("user")]
    [Authorize]
    public ActionResult GetUserInfo()
    {
        var parsedClaims = User.Claims
            .GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Type = group.Key,
                Values = group.Select(claim => claim.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated,
            Name = User.Identity?.Name,
            ParsedClaims = parsedClaims,
            RawClaims = User.Claims.Select(c => new
            {
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType
            }).ToList()
        });
    }

    [HttpGet("validate")]
    public ActionResult ValidateToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new 
            { 
                Valid = false, 
                Message = "No Bearer token provided in Authorization header" 
            });
        }
        
        return Ok(new 
        { 
            Valid = true, 
            Message = "Token validation handled by authentication middleware",
            Note = "Use GET /api/tokeninfo/info with authentication to see token details"
        });
    }

    [HttpPost("introspect")]
    [AllowAnonymous]
    public async Task<ActionResult> IntrospectToken(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] IntrospectTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var token = request?.Token;

        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                Message = "Token is required. Provide it in request body as { \"token\": \"...\" } or as Bearer token in Authorization header."
            });
        }

        try
        {
            var introspection = await _resourceService.IntrospectTokenAsync(token, cancellationToken);
            var parsedScopes = ParseDelimitedValues(introspection.Scope);
            var parsedRoles = ParseRoles(introspection.Role, introspection.Roles);
            var parsedAudiences = ParseAudiences(introspection.Aud);

            return Ok(new
            {
                introspection.Active,
                introspection.Scope,
                introspection.ClientId,
                introspection.Sub,
                introspection.Username,
                introspection.TokenType,
                introspection.Exp,
                introspection.Iat,
                introspection.Nbf,
                introspection.Iss,
                introspection.Aud,
                introspection.Jti,
                introspection.Role,
                introspection.Roles,
                ParsedScopes = parsedScopes,
                ParsedRoles = parsedRoles,
                ParsedAudiences = parsedAudiences,
                ParsedClaims = new
                {
                    Subject = introspection.Sub,
                    UserName = introspection.Username,
                    ClientId = introspection.ClientId,
                    Scope = parsedScopes,
                    Role = parsedRoles,
                    Audience = parsedAudiences,
                    Issuer = introspection.Iss,
                    JwtId = introspection.Jti
                },
                Note = "Token introspected by the resource API using configured Guardhouse introspection credentials."
            });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                Message = "Introspection request timed out.",
                Details = ex.Message,
                Hint = "Verify Guardhouse Authority is reachable and adjust Guardhouse:RequestTimeoutSeconds if needed."
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                Message = "Introspection request failed.",
                Details = ex.Message
            });
        }
    }

    public sealed class IntrospectTokenRequest
    {
        public string? Token { get; set; }
    }

    private static IReadOnlyList<string> ParseRoles(string[]? roleClaims, string? rolesClaim)
    {
        var parsedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (roleClaims is { Length: > 0 })
        {
            foreach (var roleClaim in roleClaims)
            {
                foreach (var role in ParseDelimitedValues(roleClaim))
                {
                    parsedRoles.Add(role);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(rolesClaim))
        {
            foreach (var role in ParseDelimitedValues(rolesClaim))
            {
                parsedRoles.Add(role);
            }
        }

        return parsedRoles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> ParseAudiences(IEnumerable<string>? audiences)
    {
        if (audiences == null)
        {
            return [];
        }

        return audiences
            .Where(audience => !string.IsNullOrWhiteSpace(audience))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(audience => audience, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
