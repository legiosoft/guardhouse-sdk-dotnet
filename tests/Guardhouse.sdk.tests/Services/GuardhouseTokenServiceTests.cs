using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NodaTime;
using NodaTime.Testing;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseTokenServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<GuardhouseTokenService>> _mockLogger;
    private readonly IOptions<GuardhouseClientOptions> _options;
    private readonly FakeClock _testClock;

    public GuardhouseTokenServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
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

    private GuardhouseTokenService CreateTokenService()
    {
        return new GuardhouseTokenService(
            _httpClient,
            _memoryCache,
            _options,
            _mockLogger.Object,
            _testClock);
    }

    #region Token Request Tests

    [Fact]
    public async Task RequestTokenAsync_ShouldSendCorrectRequest()
    {
        SetupMockTokenResponse(new TokenResponse
        {
            AccessToken = "test_access_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        });

        var tokenService = CreateTokenService();
        var token = await tokenService.RequestTokenAsync();

        token.AccessToken.Should().Be("test_access_token");
        token.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task RequestTokenAsync_ShouldIncludeRequiredParameters()
    {
        HttpMessageHandler? sentRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => sentRequest = req)
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new TokenResponse()))
            }));

        var tokenService = CreateTokenService();
        await tokenService.RequestTokenAsync();

        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri.Should().Be(new Uri("https://test-guardhouse.com/connect/token"));
    }

    [Fact]
    public async Task RequestTokenAsync_WhenResponseIsError_ShouldThrowException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"invalid_client\"}")
            }));

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<HttpRequestException>(() => tokenService.RequestTokenAsync());
    }

    [Fact]
    public async Task RequestTokenAsync_ShouldRetryOnFailure()
    {
        var attemptCount = 0;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.TooManyRequests
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse()))
                });
            })
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new TokenResponse()))
            }));

        var tokenService = CreateTokenService();
        await tokenService.RequestTokenAsync();

        attemptCount.Should().Be(4);
    }

    #endregion

    #region Token Refresh Tests

    [Fact]
    public async Task RefreshTokenAsync_ShouldSendCorrectRequest()
    {
        SetupMockTokenResponse(new TokenResponse
        {
            AccessToken = "new_access_token",
            RefreshToken = "new_refresh_token",
            ExpiresIn = 3600
        });

        var tokenService = CreateTokenService();
        var refreshedToken = await tokenService.RefreshTokenAsync("old_refresh_token");

        refreshedToken.AccessToken.Should().Be("new_access_token");
        refreshedToken.RefreshToken.Should().Be("new_refresh_token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenResponseIsError_ShouldThrowException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            }));

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<HttpRequestException>(() => tokenService.RefreshTokenAsync("refresh_token"));
    }

    #endregion

    #region Get Access Token Tests

    [Fact]
    public async Task GetAccessTokenAsync_WithCachedValidToken_ShouldReturnCachedToken()
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
    public async Task GetAccessTokenAsync_WithExpiredCachedToken_ShouldRequestNewToken()
    {
        var expiredToken = new TokenResponse
        {
            AccessToken = "expired_token",
            TokenType = "Bearer",
            ExpiresIn = -3600
        };

        _memoryCache.Set("guardhouse_access_token", expiredToken);

        SetupMockTokenResponse(new TokenResponse
        {
            AccessToken = "new_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        });

        var tokenService = CreateTokenService();
        var token = await tokenService.GetAccessTokenAsync();

        token.Should().Be("new_token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithRefreshToken_ShouldRefreshToken()
    {
        var cachedToken = new TokenResponse
        {
            AccessToken = "cached_token",
            TokenType = "Bearer",
            ExpiresIn = -100,
            RefreshToken = "old_refresh_token"
        };

        _memoryCache.Set("guardhouse_access_token", cachedToken);
        _memoryCache.Set("guardhouse_refresh_token", "old_refresh_token");

        SetupMockTokenResponse(new TokenResponse
        {
            AccessToken = "new_token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            RefreshToken = "new_refresh_token"
        });

        var tokenService = CreateTokenService();
        var token = await tokenService.GetAccessTokenAsync();

        token.Should().Be("new_token");
    }

    #endregion

    #region Token Introspection Tests

    [Fact]
    public async Task IntrospectTokenAsync_ShouldSendCorrectRequest()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Scope = "api read",
            ClientId = "test-client"
        });

        var tokenService = CreateTokenService();
        var introspection = await tokenService.IntrospectTokenAsync("test_token");

        introspection.Active.Should().BeTrue();
        introspection.Scope.Should().Be("api read");
        introspection.ClientId.Should().Be("test-client");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WhenTokenIsInactive_ShouldReturnInactive()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = false,
            Scope = "api read"
        });

        var tokenService = CreateTokenService();
        var introspection = await tokenService.IntrospectTokenAsync("test_token");

        introspection.Active.Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectTokenAsync_WhenResponseIsError_ShouldThrowException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"invalid_request\"}")
            }));

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<HttpRequestException>(() => tokenService.IntrospectTokenAsync("test_token"));
    }

    #endregion

    #region Is Token Active Tests

    [Fact]
    public async Task IsTokenActiveAsync_WithActiveToken_ShouldReturnTrue()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true
        });

        var tokenService = CreateTokenService();
        var isActive = await tokenService.IsTokenActiveAsync("test_token");

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsTokenActiveAsync_WithInactiveToken_ShouldReturnFalse()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = false
        });

        var tokenService = CreateTokenService();
        var isActive = await tokenService.IsTokenActiveAsync("test_token");

        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenActiveAsync_WhenIntrospectionFails_ShouldReturnFalse()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            }));

        var tokenService = CreateTokenService();
        var isActive = await tokenService.IsTokenActiveAsync("test_token");

        isActive.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private void SetupMockTokenResponse(TokenResponse tokenResponse)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            }));
    }

    private void SetupMockIntrospectionResponse(IntrospectionResponse introspectionResponse)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(introspectionResponse))
            }));
    }

    #endregion
}
