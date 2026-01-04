using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly IGuardhouseTokenService _tokenService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(IGuardhouseTokenService tokenService, ILogger<TokenController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult> GetCurrentToken()
    {
        try
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
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to request token from Guardhouse server");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error requesting token");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("refresh")]
    public async Task<ActionResult> RefreshCurrentToken()
    {
        try
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
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to refresh token from Guardhouse server");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error refreshing token");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("introspect")]
    public async Task<ActionResult> IntrospectToken([FromBody] IntrospectTokenRequest request)
    {
        try
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
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to introspect token with Guardhouse server");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error introspecting token");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpGet("check-active")]
    public async Task<ActionResult> CheckTokenActive([FromQuery] string token)
    {
        try
        {
            var isActive = await _tokenService.IsTokenActiveAsync(token);

            return Ok(new
            {
                Token = token.Substring(0, Math.Min(20, token.Length)) + "...",
                IsActive = isActive
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to check token with Guardhouse server");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking token");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }
}

public class IntrospectTokenRequest
{
    public string Token { get; set; } = string.Empty;
}
