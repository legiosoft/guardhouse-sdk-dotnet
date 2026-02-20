using Guardhouse.SDK.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExampleResource.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenInfoController : ControllerBase
{
    [HttpGet("info")]
    [Authorize]
    public ActionResult GetTokenInfo()
    {
        var accessToken = Request.Headers.Authorization.ToString();
        
        return Ok(new
        {
            Authenticated = User.Identity?.IsAuthenticated ?? false,
            UserName = User.Identity?.Name,
            AuthenticationType = User.Identity?.AuthenticationType,
            Subject = User.FindFirst("sub")?.Value,
            Issuer = User.FindFirst("iss")?.Value,
            Audience = User.FindAll("aud").Select(c => c.Value).ToList(),
            ExpiresAt = User.FindFirst("exp")?.Value,
            IssuedAt = User.FindFirst("iat")?.Value,
            Scopes = User.FindAll("scope").Select(c => c.Value).ToList(),
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            TokenType = User.FindFirst("typ")?.Value,
            JwtId = User.FindFirst("jti")?.Value,
            AccessTokenPreview = accessToken?.StartsWith("Bearer ") == true
                ? accessToken.Substring(7).GetPreview(20)
                : "N/A"
        });
    }

    [HttpGet("user")]
    [Authorize]
    public ActionResult GetUserInfo()
    {
        return Ok(new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated,
            Name = User.Identity?.Name,
            Claims = User.Claims.Select(c => new
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
}
