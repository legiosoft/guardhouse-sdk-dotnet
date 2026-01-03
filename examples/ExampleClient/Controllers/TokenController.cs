using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly IGuardhouseTokenService _tokenService;

    public TokenController(IGuardhouseTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet("current")]
    public async Task<ActionResult> GetCurrentToken()
    {
        var tokenResponse = await _tokenService.RequestTokenAsync();
        
        return Ok(new
        {
            AccessToken = tokenResponse.AccessToken,
            TokenType = tokenResponse.TokenType,
            ExpiresIn = tokenResponse.ExpiresIn,
            Scope = tokenResponse.Scope,
            AccessTokenPreview = tokenResponse.AccessToken.Substring(0, Math.Min(20, tokenResponse.AccessToken.Length)) + "..."
        });
    }

    [HttpGet("refresh")]
    public async Task<ActionResult> RefreshCurrentToken()
    {
        var tokenResponse = await _tokenService.RequestTokenAsync();
        var refreshToken = tokenResponse.RefreshToken;
        
        if (string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest(new { Message = "No refresh token available" });
        }
        
        var refreshedToken = await _tokenService.RefreshTokenAsync(refreshToken);
        
        return Ok(new
        {
            AccessToken = refreshedToken.AccessToken,
            TokenType = refreshedToken.TokenType,
            ExpiresIn = refreshedToken.ExpiresIn,
            Message = "Token refreshed successfully",
            AccessTokenPreview = refreshedToken.AccessToken.Substring(0, Math.Min(20, refreshedToken.AccessToken.Length)) + "..."
        });
    }

    [HttpPost("introspect")]
    public async Task<ActionResult> IntrospectToken([FromBody] IntrospectTokenRequest request)
    {
        var introspectionResult = await _tokenService.IntrospectTokenAsync(request.Token);
        
        return Ok(new
        {
            Active = introspectionResult.Active,
            ClientId = introspectionResult.ClientId,
            Scope = introspectionResult.Scope,
            ExpiresAt = introspectionResult.Exp,
            IssuedAt = introspectionResult.Iat,
            Subject = introspectionResult.Sub
        });
    }

    [HttpGet("check-active")]
    public async Task<ActionResult> CheckTokenActive([FromQuery] string token)
    {
        var isActive = await _tokenService.IsTokenActiveAsync(token);
        
        return Ok(new
        {
            Token = token.Substring(0, Math.Min(20, token.Length)) + "...",
            IsActive = isActive
        });
    }
}

public class IntrospectTokenRequest
{
    public string Token { get; set; } = string.Empty;
}
