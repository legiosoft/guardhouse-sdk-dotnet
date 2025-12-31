using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

/// <summary>
/// Unit tests for GuardhouseTokenService
/// </summary>
public class GuardhouseTokenServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<IGuardhouseTokenService> _mockTokenService;
    private readonly Mock<ILogger<GuardhouseTokenService>> _mockLogger;
    private readonly IOptions<GuardhouseClientOptions> _options;
    private readonly FakeClock _testClock;

    public GuardhouseTokenServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockTokenService = new Mock<IGuardhouseTokenService>();
        _mockLogger = new Mock<ILogger<GuardhouseTokenService>>();
        _testClock = new FakeClock(SystemClock.Instance.GetCurrentInstant());
        
        var options = new GuardhouseClientOptions
        {
            Authority = "https://test-guardhouse.com",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Scope = "api",
            EnableTokenCaching = true,
            CacheExpirationBufferSeconds = 60,
            EnableTokenRefresh = true,
            EnableHttpResilience = true,
            RequestTimeoutSeconds = 30,
            MaxRetryAttempts = 3
        };

        _options = Options.Create(options);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithCachedToken_ShouldReturnCachedToken()
    {
        var cachedToken = new TokenResponse
        {
            AccessToken = "cached_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        _memoryCache.Set("guardhouse_access_token", cachedToken);

        var tokenService = CreateTokenService();
        var token = await tokenService.GetAccessTokenAsync();

        token.Should().Be("cached_token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithExpiredCacheToken_ShouldRequestNewToken()
    {
        var expiredToken = new TokenResponse
        {
            AccessToken = "expired_token",
            TokenType = "Bearer",
            ExpiresIn = -60 // Negative for expired
        };

        _memoryCache.Set("guardhouse_access_token", expiredToken);

        var newToken = new TokenResponse
        {
            AccessToken = "new_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        SetupMockTokenResponse(newToken);

        var tokenService = CreateTokenService();
        var token = await tokenService.GetAccessTokenAsync();

        token.Should().Be("new_token");
    }

    [Fact]
    public async Task RequestTokenAsync_ShouldReturnTokenFromServer()
    {
        var expectedToken = new TokenResponse
        {
            AccessToken = "server_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        SetupMockTokenResponse(expectedToken);

        var tokenService = CreateTokenService();
        var token = await tokenService.RequestTokenAsync();

        token.Should().BeEquivalentTo(expectedToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewToken()
    {
        var refreshToken = "refresh_token_123";
        var newToken = new TokenResponse
        {
            AccessToken = "refreshed_token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            RefreshToken = "new_refresh_token_456"
        };

        SetupMockTokenResponse(newToken);

        var tokenService = CreateTokenService();
        var token = await tokenService.RefreshTokenAsync(refreshToken);

        token.AccessToken.Should().Be("refreshed_token");
        token.RefreshToken.Should().Be("new_refresh_token_456");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithActiveToken_ShouldReturnActiveResponse()
    {
        var activeToken = "active_token";
        var expectedResponse = new IntrospectionResponse
        {
            Active = true,
            ClientId = "test-client",
            Username = "testuser",
            Scope = "api read",
            TokenType = "Bearer",
            Exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Sub = "user123",
            Aud = "test-audience",
            Iss = "https://test-guardhouse.com"
        };

        SetupMockIntrospectionResponse(activeToken, expectedResponse);

        var tokenService = CreateTokenService();
        var response = await tokenService.IntrospectTokenAsync(activeToken);

        response.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithInactiveToken_ShouldReturnInactiveResponse()
    {
        var inactiveToken = "inactive_token";
        var expectedResponse = new IntrospectionResponse
        {
            Active = false,
            ClientId = null,
            Username = null,
            Scope = null,
            Exp = null,
            Iat = null,
            Sub = null
        };

        SetupMockIntrospectionResponse(inactiveToken, expectedResponse);

        var tokenService = CreateTokenService();
        var response = await tokenService.IntrospectTokenAsync(inactiveToken);

        response.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task IsTokenActiveAsync_WithActiveToken_ShouldReturnTrue()
    {
        var activeToken = "active_token";

        SetupMockIntrospectionResponse(activeToken, new IntrospectionResponse { Active = true });

        var tokenService = CreateTokenService();
        var isActive = await tokenService.IsTokenActiveAsync(activeToken);

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithCancellation_ShouldBeRespected()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        cts.Dispose();

        var tokenService = CreateTokenService();

        await tokenService.Invoking(async ts => await ts.GetAccessTokenAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    private GuardhouseTokenService CreateTokenService()
    {
        return new GuardhouseTokenService(
            _httpClient,
            _memoryCache,
            _options,
            _mockLogger.Object,
            _testClock);
    }

    private void SetupMockTokenResponse(TokenResponse tokenResponse)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(tokenResponse), System.Text.Encoding.UTF8, "application/json");
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>(),
                ItExpr.IsAny<StringContent>())
            )
            .Returns(Task.FromResult(response))
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>(),
                ItExpr.IsAny<StringContent>())
            )
            .Returns(Task.FromResult(response));
    }

    private void SetupMockIntrospectionResponse(string token, IntrospectionResponse introspectionResponse)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(introspectionResponse), System.Text.Encoding.UTF8, "application/json");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<StringContent>())
            )
            .Returns(Task.FromResult(response));
    }
}