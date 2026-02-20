using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using ExampleClient.Utilities;

namespace ExampleClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly IGuardhouseTokenService _tokenService;
    private readonly ILogger<TokenController> _logger;
    private readonly IWebHostEnvironment _environment;

    public TokenController(IGuardhouseTokenService tokenService, ILogger<TokenController> logger, IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet("current")]
    public async Task<ActionResult> GetCurrentToken()
    {
        try
        {
            var tokenResponse = await _tokenService.RequestTokenAsync();
            var scopeFromToken = tokenResponse.AccessToken.GetTokenScope();

            return Ok(new
            {
                TokenType = tokenResponse.TokenType,
                ExpiresIn = tokenResponse.ExpiresIn,
                Scope = scopeFromToken ?? tokenResponse.Scope,
                AccessTokenPreview = tokenResponse.AccessToken.GetPreview(20)
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
            var accessToken = await _tokenService.GetAccessTokenAsync();
            var scopeFromToken = accessToken.GetTokenScope();

            return Ok(new
            {
                AccessToken = accessToken,
                Scope = scopeFromToken,
                Message = "Current access token retrieved (auto-refreshed if needed by SDK)",
                AccessTokenPreview = accessToken.GetPreview(20),
                Note = "SDK automatically handles token refresh when EnableTokenRefresh = true. This endpoint demonstrates getting the current cached token."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting access token");
            return StatusCode(500, new { Message = "An unexpected error occurred", Error = ex.Message });
        }
    }

    [HttpPost("introspect")]
    public async Task<ActionResult> IntrospectToken([FromBody] IntrospectTokenRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

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
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var isActive = await _tokenService.IsTokenActiveAsync(token);

            return Ok(new
            {
                Token = token.GetPreview(20),
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
