using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
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
        HttpRequestMessage? sentRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => sentRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new TokenResponse()))
            });

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
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"invalid_client\"}")
            });

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.RequestTokenAsync());
    }

    [Fact]
    public async Task RequestTokenAsync_ShouldRetryOnFailure()
    {
        var attemptCount = 0;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount < 4)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.TooManyRequests
                    };
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new TokenResponse()))
                };
            });

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
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.RefreshTokenAsync("refresh_token"));
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

        _memoryCache.Set("guardhouse_access_token_test-client", cachedToken);

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

        _memoryCache.Set("guardhouse_access_token_test-client", expiredToken);

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

        _memoryCache.Set("guardhouse_access_token_test-client", cachedToken);
        _memoryCache.Set("guardhouse_refresh_token_test-client", "old_refresh_token");

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

    #region Cache Key Security Tests

    [Fact]
    public async Task GetAccessTokenAsync_DifferentClients_ShouldNotShareCache()
    {
        var cachedTokenForClient1 = new TokenResponse
        {
            AccessToken = "client1_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        var options1 = new GuardhouseClientOptions
        {
            Authority = "https://test-guardhouse.com",
            ClientId = "client1",
            ClientSecret = "test-secret",
            Scope = "api",
            EnableTokenCaching = true,
            CacheExpirationBufferSeconds = 60
        };
        var options1Wrapper = Options.Create(options1);

        var service1 = new GuardhouseTokenService(_httpClient, _memoryCache, options1Wrapper, _mockLogger.Object, _testClock);

        var cachedTokenForClient2 = new TokenResponse
        {
            AccessToken = "client2_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        var options2 = new GuardhouseClientOptions
        {
            Authority = "https://test-guardhouse.com",
            ClientId = "client2",
            ClientSecret = "test-secret",
            Scope = "api",
            EnableTokenCaching = true,
            CacheExpirationBufferSeconds = 60
        };
        var options2Wrapper = Options.Create(options2);

        var service2 = new GuardhouseTokenService(_httpClient, _memoryCache, options2Wrapper, _mockLogger.Object, _testClock);

        _memoryCache.Set("guardhouse_access_token_client1", cachedTokenForClient1);
        _memoryCache.Set("guardhouse_access_token_client2", cachedTokenForClient2);

        var token1 = await service1.GetAccessTokenAsync();
        var token2 = await service2.GetAccessTokenAsync();

        token1.Should().Be("client1_token");
        token2.Should().Be("client2_token");
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
    public async Task IntrospectTokenAsync_ShouldUseBasicAuthentication()
    {
        HttpRequestMessage? sentRequest = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => sentRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new IntrospectionResponse { Active = true }))
            });

        var tokenService = CreateTokenService();
        await tokenService.IntrospectTokenAsync("test_token");

        sentRequest.Should().NotBeNull();
        sentRequest!.Headers.Authorization.Should().NotBeNull();
        sentRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        var expectedCredentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-client:test-secret"));
        sentRequest.Headers.Authorization.Parameter.Should().Be(expectedCredentials);
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldNotIncludeCredentialsInFormBody()
    {
        string? contentData = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                contentData = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new IntrospectionResponse { Active = true }))
            });

        var tokenService = CreateTokenService();
        await tokenService.IntrospectTokenAsync("test_token");

        contentData.Should().NotBeNull();
        contentData.Should().NotContain("client_id");
        contentData.Should().NotContain("client_secret");
        contentData.Should().Contain("token=test_token");
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
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"invalid_request\"}")
            });

        var tokenService = CreateTokenService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.IntrospectTokenAsync("test_token"));
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
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var tokenService = CreateTokenService();
        var isActive = await tokenService.IsTokenActiveAsync("test_token");

        isActive.Should().BeFalse();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task GetAccessTokenAsync_WithConcurrentRequests_ShouldPreventThunderingHerd()
    {
        var expiredToken = new TokenResponse
        {
            AccessToken = "expired_token",
            TokenType = "Bearer",
            ExpiresIn = -3600
        };

        _memoryCache.Set("guardhouse_access_token_test-client", expiredToken);

        var requestCount = 0;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                Interlocked.Increment(ref requestCount);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new TokenResponse
                {
                    AccessToken = "new_token",
                    TokenType = "Bearer",
                    ExpiresIn = 3600
                }))
            });

        var tokenService = CreateTokenService();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => tokenService.GetAccessTokenAsync())
            .ToArray();

        await Task.WhenAll(tasks);

        requestCount.Should().Be(1, "Only one HTTP request should be made despite 10 concurrent calls");
        tasks.All(t => t.Result == "new_token").Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private void SetupMockTokenResponse(TokenResponse tokenResponse)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });
    }

    private void SetupMockIntrospectionResponse(IntrospectionResponse introspectionResponse)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(introspectionResponse))
            });
    }

    #endregion
}
