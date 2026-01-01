using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenInfoController : ControllerBase
{
    [HttpGet("claims")]
    [Authorize]
    public ActionResult GetTokenClaims()
    {
        var claims = User.Claims.Select(c => new
        {
            Type = c.Type,
            Value = c.Value,
            ValueType = c.ValueType
        }).ToList();

        return Ok(new
        {
            Authenticated = User.Identity?.IsAuthenticated ?? false,
            UserName = User.Identity?.Name,
            AuthenticationType = User.Identity?.AuthenticationType,
            Claims = claims
        });
    }

    [HttpGet("user-info")]
    [Authorize]
    public ActionResult GetUserInfo()
    {
        return Ok(new
        {
            Subject = User.FindFirst("sub")?.Value,
            Name = User.FindFirst("name")?.Value,
            Email = User.FindFirst("email")?.Value,
            EmailVerified = bool.TryParse(User.FindFirst("email_verified")?.Value, out var verified) ? (bool?)verified : null,
            Roles = User.FindAll("role").Select(c => c.Value).ToList(),
            Scopes = User.FindAll("scope").Select(c => c.Value).ToList(),
            IssuedAt = User.FindFirst("iat")?.Value,
            Expiration = User.FindFirst("exp")?.Value,
            Issuer = User.FindFirst("iss")?.Value,
            Audience = User.FindFirst("aud")?.Value
        });
    }

    [HttpGet("authorize-info")]
    [Authorize]
    public ActionResult GetAuthorizationInfo()
    {
        var policies = new
        {
            HasReadAccess = User.HasClaim("scope", "read") || User.HasClaim("scope", "api"),
            HasWriteAccess = User.HasClaim("scope", "write") || User.HasClaim("scope", "admin"),
            IsAdmin = User.HasClaim("scope", "admin"),
            Roles = User.FindAll("role").Select(c => c.Value).ToList(),
            Scopes = User.FindAll("scope").Select(c => c.Value).ToList()
        };

        return Ok(new
        {
            User = User.Identity?.Name,
            Policies = policies
        });
    }
}
