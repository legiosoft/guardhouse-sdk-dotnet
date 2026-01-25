using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        
        _logger.LogInformation("Auth Header Debug:");
        _logger.LogInformation("  AuthHeader: {AuthHeader}", 
            string.IsNullOrEmpty(authHeader) ? "NULL" : authHeader);
        _logger.LogInformation("  IsAuthenticated: {IsAuthenticated}", isAuthenticated);
        _logger.LogInformation("  User.Identity: {Identity}", User.Identity?.Name ?? "NULL");
        
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

            _logger.LogInformation("Decoded Token:");
            _logger.LogInformation("  Issuer: {Issuer}", jwtToken.Issuer);
            _logger.LogInformation("  Issuer (trimmed): {Issuer}", jwtToken.Issuer?.TrimEnd('/'));
            _logger.LogInformation("  Audience: {Audience}", jwtToken.Audiences.FirstOrDefault());
            _logger.LogInformation("  ValidFrom: {ValidFrom}", jwtToken.ValidFrom);
            _logger.LogInformation("  ValidTo: {ValidTo}", jwtToken.ValidTo);
            _logger.LogInformation("  Algorithm: {Algorithm}", jwtToken.Header.Alg);

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
        
        _logger.LogInformation("User Claims Debug:");
        _logger.LogInformation("  IsAuthenticated: {IsAuthenticated}", isAuthenticated);
        _logger.LogInformation("  AuthenticationType: {AuthType}", User.Identity?.AuthenticationType);

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

            _logger.LogInformation("Token Expiry Check:");
            _logger.LogInformation("  Now: {Now}", now);
            _logger.LogInformation("  ValidTo: {ValidTo}", jwtToken.ValidTo);
            _logger.LogInformation("  IsExpired: {IsExpired}", isExpired);
            _logger.LogInformation("  TimeUntilExpiry: {TimeUntilExpiry}", timeUntilExpiry);

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
