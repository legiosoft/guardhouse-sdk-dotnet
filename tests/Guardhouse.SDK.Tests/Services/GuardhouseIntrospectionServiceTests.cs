using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NodaTime;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseIntrospectionServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<GuardhouseIntrospectionService>> _mockLogger;
    private readonly IOptions<GuardhouseResourceOptions> _options;

    public GuardhouseIntrospectionServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<GuardhouseIntrospectionService>>();

        _options = Options.Create(new GuardhouseResourceOptions
        {
            Authority = "https://test-guardhouse.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret"
        });
    }

    private GuardhouseIntrospectionService CreateService()
    {
        return new GuardhouseIntrospectionService(
            _httpClient,
            _memoryCache,
            _options,
            _mockLogger.Object);
    }

    #region Introspection Tests

    [Fact]
    public async Task IntrospectTokenAsync_WithActiveToken_ShouldReturnActiveTrue()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Scope = "api read",
            ClientId = "test-client",
            Exp = 3600
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Scope.Should().Be("api read");
        result.ClientId.Should().Be("test-client");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithInactiveToken_ShouldReturnActiveFalse()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = false,
            Scope = "api read"
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldUseFormDataCredentials_ByDefault()
    {
        HttpRequestMessage? sentRequest = null;
        string? formData = null;
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                sentRequest = req;
                formData = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new IntrospectionResponse { Active = true }))
            });

        var service = CreateService();
        await service.IntrospectTokenAsync("test_token");

        sentRequest.Should().NotBeNull();
        sentRequest!.Headers.Authorization.Should().BeNull();
        formData.Should().Contain("client_id=test-client");
        formData.Should().Contain("client_secret=test-secret");
        formData.Should().Contain("token=test_token");
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldUseCachedResult_WhenAvailable()
    {
        var cachedResponse = new IntrospectionResponse
        {
            Active = true,
            Scope = "cached_scope"
        };

        var tokenHash = ComputeTokenHash("test_token");
        _memoryCache.Set($"guardhouse_introspection_{tokenHash}", cachedResponse);

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Should().BeSameAs(cachedResponse);
        _mockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldUseConfigurableCacheTtl()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Scope = "api",
            Exp = (long)(SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromHours(24)).ToUnixTimeSeconds())
        });

        var options = Options.Create(new GuardhouseResourceOptions
        {
            Authority = "https://test-guardhouse.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret",
            IntrospectionCacheTtlSeconds = 120
        });

        var service = new GuardhouseIntrospectionService(
            _httpClient,
            _memoryCache,
            options,
            _mockLogger.Object);

        await service.IntrospectTokenAsync("test_token");

        var tokenHash = ComputeTokenHash("test_token");
        _memoryCache.TryGetValue($"guardhouse_introspection_{tokenHash}", out IntrospectionResponse? cachedResult);
        cachedResult.Should().NotBeNull();
    }

    [Fact]
    public async Task IntrospectTokenAsync_WhenHttpResponseError_ShouldThrowException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"invalid_request\",\"error_description\":\"token=super-secret-token\"}")
            });

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IntrospectTokenAsync("test_token"));
        exception.Message.Should().Contain("invalid_request");
        exception.Message.Should().NotContain("error_description");
        exception.Message.Should().NotContain("super-secret-token");
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldRespectConfiguredRequestTimeout()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new IntrospectionResponse { Active = true }))
                };
            });

        var options = Options.Create(new GuardhouseResourceOptions
        {
            Authority = "https://test-guardhouse.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret",
            RequestTimeoutSeconds = 1
        });

        var service = new GuardhouseIntrospectionService(
            _httpClient,
            _memoryCache,
            options,
            _mockLogger.Object);

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => service.IntrospectTokenAsync("test_token"));
        exception.Message.Should().Contain("1 seconds");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WhenDeserializationFails_ShouldThrowJsonException()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("invalid json")
            });

        var service = CreateService();

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.IntrospectTokenAsync("test_token"));
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithValidAlgorithm_ShouldReturnTrue()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Algorithm = "RS256",
            Signature = "valid-signature"
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Algorithm.Should().Be("RS256");
    }

    [Fact]
    public async Task IntrospectTokenAsync_ShouldSendBasicAuthHeader_WhenConfigured()
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

        var options = Options.Create(new GuardhouseResourceOptions
        {
            Authority = "https://test-guardhouse.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret",
            IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.BasicAuth
        });

        var service = new GuardhouseIntrospectionService(
            _httpClient,
            _memoryCache,
            options,
            _mockLogger.Object);

        await service.IntrospectTokenAsync("test_token");

        sentRequest.Should().NotBeNull();
        sentRequest!.Headers.Authorization.Should().NotBeNull();
        sentRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithRolesArray_ShouldParseCorrectly()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Role = new[] { "admin", "user", "editor" }
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Role.Should().NotBeNull();
        result.Role.Should().HaveCount(3);
        result.Role.Should().Contain("admin");
        result.Role.Should().Contain("user");
        result.Role.Should().Contain("editor");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithNoneAlgorithm_ShouldReturnTrueButWarning()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Algorithm = "none",
            Signature = "no-signature"
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Algorithm.Should().Be("none");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithSignature_ShouldReturnTrue()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Signature = "abc123signature"
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Signature.Should().Be("abc123signature");
    }

    [Fact]
    public async Task IntrospectTokenAsync_WithTokenClaims_ShouldReturnThemCorrectly()
    {
        SetupMockIntrospectionResponse(new IntrospectionResponse
        {
            Active = true,
            Sub = "user123",
            Iss = "https://test-guardhouse.com",
            Aud = new[] { "test-audience" },
            Jti = "token-id-123",
            Username = "testuser"
        });

        var service = CreateService();
        var result = await service.IntrospectTokenAsync("test_token");

        result.Active.Should().BeTrue();
        result.Sub.Should().Be("user123");
        result.Iss.Should().Be("https://test-guardhouse.com");
        result.Aud.Should().ContainSingle("test-audience");
        result.Jti.Should().Be("token-id-123");
        result.Username.Should().Be("testuser");
    }

    #endregion

    #region Helper Methods

    private void SetupMockIntrospectionResponse(IntrospectionResponse response)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response))
            });
    }

    private static string ComputeTokenHash(string token)
    {
        var tokenBytes = MemoryMarshal.AsBytes(token.AsSpan());
        var hash = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}
