using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

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
            Audience = User.FindFirst("aud")?.Value,
            ExpiresAt = User.FindFirst("exp")?.Value,
            Scope = User.FindAll("scope").Select(c => c.Value).ToList(),
            AccessTokenPreview = accessToken?.Substring(0, Math.Min(50, accessToken.Length)) + "..."
        });
    }
}
