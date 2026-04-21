using System.Net;
using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Users;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseUserServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IGuardhouseTokenService> _mockTokenService;
    private readonly Mock<ILogger<GuardhouseUserService>> _mockLogger;
    private readonly IOptions<GuardhouseUserOptions> _userOptions;
    private readonly IOptions<GuardhouseClientOptions> _clientOptions;

    public GuardhouseUserServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockTokenService = new Mock<IGuardhouseTokenService>();
        _mockLogger = new Mock<ILogger<GuardhouseUserService>>();

        _mockTokenService
            .Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-access-token");

        _userOptions = Options.Create(new GuardhouseUserOptions
        {
            ApiBaseUrl = "https://api.guardhouse.test"
        });

        _clientOptions = Options.Create(new GuardhouseClientOptions
        {
            Authority = "https://authority.guardhouse.test",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            IncludeOfflineAccessScope = true
        });
    }

    [Fact]
    public async Task CreateUserAsync_ShouldSendExpectedContract()
    {
        HttpRequestMessage? sentRequest = null;
        string? requestBody = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                sentRequest = request;
                requestBody = await request.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"userId\":42}")
            });

        var service = CreateService();
        var response = await service.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "StrongPassword123!"
        });

        response.UserId.Should().Be(42);

        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users"));
        sentRequest.Headers.Authorization.Should().NotBeNull();
        sentRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        sentRequest.Headers.Authorization.Parameter.Should().Be("test-access-token");

        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("firstName").GetString().Should().Be("Ada");
        bodyDocument.RootElement.GetProperty("lastName").GetString().Should().Be("Lovelace");
        bodyDocument.RootElement.GetProperty("email").GetString().Should().Be("ada@example.com");
        bodyDocument.RootElement.GetProperty("password").GetString().Should().Be("StrongPassword123!");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserNotFound_ShouldReturnNull()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var service = CreateService();
        var result = await service.GetUserByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserNotFound_ShouldReturnFalse()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var service = CreateService();
        var result = await service.UpdateUserAsync(999, new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "User",
            Email = "updated@example.com"
        });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserNotFound_ShouldReturnFalse()
    {
        HttpRequestMessage? sentRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = CreateUsersClient();
        var result = await client.DeleteUserAsync(999);

        result.Should().BeFalse();
        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Delete);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users/999"));
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenBadRequest_ShouldNotExposeRawResponseBody()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Current password 'wrong' is invalid for ada@example.com")
            });

        var service = CreateService();

        Func<Task> act = () => service.ChangePasswordAsync(42, new ChangePasswordRequest
        {
            CurrentPassword = "wrong",
            NewPassword = "NewStrongPassword123!"
        });

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("BadRequest");
        exception.Which.Message.Should().Contain("Response body omitted for security.");
        exception.Which.Message.Should().NotContain("wrong");
        exception.Which.Message.Should().NotContain("ada@example.com");
    }

    [Fact]
    public async Task CreateUserAsync_WhenConflict_ShouldNotExposeRawResponseBody()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Conflict,
                Content = new StringContent("User ada@example.com already exists with password hash abc123")
            });

        var service = CreateService();

        Func<Task> act = () => service.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com"
        });

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Conflict");
        exception.Which.Message.Should().Contain("Response body omitted for security.");
        exception.Which.Message.Should().NotContain("ada@example.com");
        exception.Which.Message.Should().NotContain("abc123");
    }

    [Fact]
    public async Task GetUserByIdAsync_WithUnknownStatus_ShouldFallbackToUnknown()
    {
        const string json = """
                            {
                              "id": 7,
                              "email": "john@example.com",
                              "firstName": "John",
                              "lastName": "Doe",
                              "status": "retired",
                              "roles": [],
                              "systemPermissions": []
                            }
                            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var service = CreateService();
        var response = await service.GetUserByIdAsync(7);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Unknown);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenApiBaseUrlIsEmpty_ShouldFallbackToClientAuthority()
    {
        HttpRequestMessage? sentRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var service = CreateService(Options.Create(new GuardhouseUserOptions()));
        _ = await service.GetUserByIdAsync(15);

        sentRequest.Should().NotBeNull();
        sentRequest!.RequestUri.Should().Be(new Uri("https://authority.guardhouse.test/api/v1/users/15"));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenServerFails_ShouldExposeOnlySafeErrorCode()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\":\"server_error\",\"detail\":\"password=SuperSecret123!\"}")
            });

        var service = CreateService();

        Func<Task> act = () => service.UpdateUserAsync(42, new UpdateUserRequest
        {
            FirstName = "Updated",
            LastName = "User",
            Email = "updated@example.com"
        });

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("InternalServerError");
        exception.Which.Message.Should().Contain("500");
        exception.Which.Message.Should().Contain("server_error");
        exception.Which.Message.Should().NotContain("detail");
        exception.Which.Message.Should().NotContain("SuperSecret123!");
    }

    [Fact]
    public void CreateService_WhenRequireHttpsIsTrueAndApiBaseUrlIsHttp_ShouldThrow()
    {
        var insecureUserOptions = Options.Create(new GuardhouseUserOptions
        {
            ApiBaseUrl = "http://insecure.guardhouse.test"
        });

        var strictClientOptions = Options.Create(new GuardhouseClientOptions
        {
            Authority = "https://authority.guardhouse.test",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            RequireHttps = true,
            IncludeOfflineAccessScope = true
        });

        Action act = () => _ = CreateService(insecureUserOptions, strictClientOptions);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HTTPS is required for Guardhouse user API endpoint*");
    }

    private GuardhouseUserService CreateService(
        IOptions<GuardhouseUserOptions>? userOptions = null,
        IOptions<GuardhouseClientOptions>? clientOptions = null)
    {
        return new GuardhouseUserService(
            _httpClient,
            _mockTokenService.Object,
            userOptions ?? _userOptions,
            clientOptions ?? _clientOptions,
            _mockLogger.Object);
    }

    private GuardhouseUsersClient CreateUsersClient(
        IOptions<GuardhouseUserOptions>? userOptions = null,
        IOptions<GuardhouseClientOptions>? clientOptions = null)
    {
        return new GuardhouseUsersClient(
            _httpClient,
            _mockTokenService.Object,
            userOptions ?? _userOptions,
            clientOptions ?? _clientOptions,
            _mockLogger.Object);
    }
}
