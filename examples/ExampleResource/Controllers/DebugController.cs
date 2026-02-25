using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ExampleResource.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly ILogger<DebugController> _logger;

    public DebugController(ILogger<DebugController> logger)
    {
        _logger = logger;
    }

    [HttpGet("config")]
    public ActionResult GetConfig([FromServices] IConfiguration configuration)
    {
        var authority = configuration["Guardhouse:Authority"];
        var audience = configuration["Guardhouse:Audience"];
        var validationMode = configuration["Guardhouse:ValidationMode"];

        return Ok(new
        {
            Authority = authority,
            AuthorityNormalized = authority?.TrimEnd('/'),
            Audience = audience,
            ValidationMode = validationMode ?? "JwtSignature (default)",
            Note = "SDK automatically trims trailing slash from Authority for validation"
        });
    }

    [HttpGet("auth-header")]
    public ActionResult GetAuthHeader()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Auth Header Debug:");
            _logger.LogDebug("  AuthHeader: {AuthHeader}",
                string.IsNullOrEmpty(authHeader) ? "NULL" : authHeader);
            _logger.LogDebug("  IsAuthenticated: {IsAuthenticated}", isAuthenticated);
            _logger.LogDebug("  User.Identity: {Identity}", User.Identity?.Name ?? "NULL");
        }
        
        return Ok(new
        {
            AuthHeader = string.IsNullOrEmpty(authHeader) ? "NULL" : authHeader,
            HasAuthHeader = !string.IsNullOrEmpty(authHeader),
            IsAuthenticated = isAuthenticated,
            AuthType = User.Identity?.AuthenticationType,
            UserName = User.Identity?.Name
        });
    }

    [HttpGet("decode-token")]
    public ActionResult DecodeToken([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { Message = "Token parameter is required" });
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var claims = jwtToken.Claims.Select(c => new
            {
                Type = c.Type,
                Value = c.Value
            }).ToList();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Decoded Token:");
                _logger.LogDebug("  Issuer: {Issuer}", jwtToken.Issuer);
                _logger.LogDebug("  Issuer (trimmed): {Issuer}", jwtToken.Issuer?.TrimEnd('/'));
                _logger.LogDebug("  Audience: {Audience}", jwtToken.Audiences.FirstOrDefault());
                _logger.LogDebug("  ValidFrom: {ValidFrom}", jwtToken.ValidFrom);
                _logger.LogDebug("  ValidTo: {ValidTo}", jwtToken.ValidTo);
                _logger.LogDebug("  Algorithm: {Algorithm}", jwtToken.Header.Alg);
            }

            return Ok(new
            {
                Header = new
                {
                    Alg = jwtToken.Header.Alg,
                    Typ = jwtToken.Header.Typ,
                    Kid = jwtToken.Header.Kid
                },
                Payload = new
                {
                    Issuer = jwtToken.Issuer,
                    IssuerTrimmed = jwtToken.Issuer?.TrimEnd('/'),
                    Audience = jwtToken.Audiences.FirstOrDefault(),
                    Subject = jwtToken.Subject,
                    ValidFrom = jwtToken.ValidFrom,
                    ValidTo = jwtToken.ValidTo,
                    IssuedAt = jwtToken.IssuedAt,
                    Expires = jwtToken.ValidTo,
                    Now = DateTime.UtcNow
                },
                Claims = claims
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode token");
            return BadRequest(new { Message = "Failed to decode token", Error = ex.Message });
        }
    }

    [HttpGet("user-claims")]
    [Authorize]
    public ActionResult GetUserClaims()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("User Claims Debug:");
            _logger.LogDebug("  IsAuthenticated: {IsAuthenticated}", isAuthenticated);
            _logger.LogDebug("  AuthenticationType: {AuthType}", User.Identity?.AuthenticationType);
        }

        var claims = User.Claims.Select(c => new
        {
            Type = c.Type,
            Value = c.Value,
            ValueType = c.ValueType
        }).ToList();

        return Ok(new
        {
            IsAuthenticated = isAuthenticated,
            AuthenticationType = User.Identity?.AuthenticationType,
            Name = User.Identity?.Name,
            Scopes = User.FindAll("scope")
                .Concat(User.FindAll("scp"))
                .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Roles = User.FindAll(ClaimTypes.Role)
                .Concat(User.FindAll("role"))
                .Concat(User.FindAll("roles"))
                .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ParsedClaims = User.Claims
                .GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Type = group.Key,
                    Values = group.Select(claim => claim.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .OrderBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Claims = claims
        });
    }

    [HttpGet("check-expiry")]
    public ActionResult CheckExpiry([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { Message = "Token parameter is required" });
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            
            var now = DateTime.UtcNow;
            var isExpired = now > jwtToken.ValidTo;
            var timeUntilExpiry = jwtToken.ValidTo - now;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Token Expiry Check:");
                _logger.LogDebug("  Now: {Now}", now);
                _logger.LogDebug("  ValidTo: {ValidTo}", jwtToken.ValidTo);
                _logger.LogDebug("  IsExpired: {IsExpired}", isExpired);
                _logger.LogDebug("  TimeUntilExpiry: {TimeUntilExpiry}", timeUntilExpiry);
            }

            return Ok(new
            {
                Now = now,
                ValidTo = jwtToken.ValidTo,
                IsExpired = isExpired,
                TimeUntilExpiryMinutes = timeUntilExpiry.TotalMinutes,
                TimeUntilExpiry = timeUntilExpiry.ToString(@"hh\:mm\:ss")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check token expiry");
            return BadRequest(new { Message = "Failed to check token expiry", Error = ex.Message });
        }
    }
}
