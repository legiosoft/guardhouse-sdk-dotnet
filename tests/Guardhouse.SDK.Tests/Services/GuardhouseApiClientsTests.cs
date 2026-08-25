using System.Net;
using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Permissions;
using Guardhouse.SDK.Models.Roles;
using Guardhouse.SDK.Models.Users;
using Guardhouse.SDK.Models.Users.Privacy;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NodaTime;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseApiClientsTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IGuardhouseTokenService> _mockTokenService;
    private readonly IOptions<GuardhouseUserOptions> _userOptions;
    private readonly IOptions<GuardhouseClientOptions> _clientOptions;

    public GuardhouseApiClientsTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockTokenService = new Mock<IGuardhouseTokenService>();

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

        var client = CreateUsersClient();
        var response = await client.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            SendInvite = true,
            TriggerWebhook = true,
            RedirectUrl = "https://app.example.com/invite",
            InviterName = "Grace Hopper"
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
        bodyDocument.RootElement.GetProperty("sendInvite").GetBoolean().Should().BeTrue();
        bodyDocument.RootElement.GetProperty("triggerWebhook").GetBoolean().Should().BeTrue();
        bodyDocument.RootElement.GetProperty("redirectUrl").GetString().Should().Be("https://app.example.com/invite");
        bodyDocument.RootElement.GetProperty("inviterName").GetString().Should().Be("Grace Hopper");
    }

    [Fact]
    public async Task GetUsersAsync_ShouldBuildExpectedQueryString()
    {
        HttpRequestMessage? sentRequest = null;

        const string json = """
                            {
                              "total": 1,
                              "users": [
                                {
                                  "id": 7,
                                  "firstName": "John",
                                  "lastName": "Doe",
                                  "email": "john@example.com",
                                  "roles": [],
                                  "permissions": [],
                                  "lastLogin": "2026-04-22T10:15:30Z",
                                  "status": "active",
                                  "isSuspended": false,
                                  "isLocked": false,
                                  "avatarUrl": null
                                }
                              ]
                            }
                            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = CreateUsersClient();
        var response = await client.GetUsersAsync(new GetUsersRequest
        {
            PageSize = 25,
            Offset = 50,
            Email = "john@example.com"
        });

        response.Total.Should().Be(1);
        response.Users.Should().HaveCount(1);
        response.Users[0].LastLogin.Should().Be(Instant.FromUtc(2026, 4, 22, 10, 15, 30));
        response.Users[0].Status.Should().Be(UserStatus.Active);
        response.Users[0].IsSuspended.Should().BeFalse();
        response.Users[0].IsLocked.Should().BeFalse();

        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Get);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users?pageSize=25&offset=50&email=john%40example.com"));
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSendExpectedContract()
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
                StatusCode = HttpStatusCode.NoContent
            });

        var client = CreateUsersClient();
        var result = await client.ChangePasswordAsync(42, new ChangePasswordRequest
        {
            CurrentPassword = "CurrentStrongPassword123!",
            NewPassword = "NewStrongPassword123!"
        });

        result.Should().BeTrue();
        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users/42/password"));

        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("currentPassword").GetString().Should().Be("CurrentStrongPassword123!");
        bodyDocument.RootElement.GetProperty("newPassword").GetString().Should().Be("NewStrongPassword123!");
    }

    [Fact]
    public async Task ChangePasswordAsync_ForPasswordlessUser_ShouldSerializeNullCurrentPassword()
    {
        string? requestBody = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NoContent
            });

        var client = CreateUsersClient();
        var result = await client.ChangePasswordAsync(42, new ChangePasswordRequest
        {
            CurrentPassword = null,
            NewPassword = "NewStrongPassword123!"
        });

        result.Should().BeTrue();
        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("currentPassword").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenUserDoesNotExist_ShouldReturnFalse()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = CreateUsersClient();
        var result = await client.ChangePasswordAsync(999, new ChangePasswordRequest
        {
            CurrentPassword = "CurrentStrongPassword123!",
            NewPassword = "NewStrongPassword123!"
        });

        result.Should().BeFalse();
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

        var client = CreateUsersClient();

        Func<Task> act = () => client.ChangePasswordAsync(42, new ChangePasswordRequest
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
    public async Task RequestEmailChangeAsync_ShouldSendExpectedContract()
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
                StatusCode = HttpStatusCode.NoContent
            });

        var client = CreateUsersClient();
        var result = await client.RequestEmailChangeAsync(42, new RequestEmailChangeRequest
        {
            NewEmail = "ada.updated@example.com"
        });

        result.Should().BeTrue();
        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users/42/email"));

        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("newEmail").GetString().Should().Be("ada.updated@example.com");
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldDeserializeStatusAndAccessFlagsSeparately()
    {
        const string json = """
                            {
                              "id": 7,
                              "email": "john@example.com",
                              "firstName": "John",
                              "lastName": "Doe",
                              "lastLogin": "2026-04-22T10:15:30Z",
                              "status": 2,
                              "isSuspended": true,
                              "isLocked": true,
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

        var client = CreateUsersClient();
        var response = await client.GetUserByIdAsync(7);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Invited);
        response.IsSuspended.Should().BeTrue();
        response.IsLocked.Should().BeTrue();
        response.LastLogin.Should().Be(Instant.FromUtc(2026, 4, 22, 10, 15, 30));
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

        var client = CreateUsersClient(Options.Create(new GuardhouseUserOptions()));
        _ = await client.GetUserByIdAsync(15);

        sentRequest.Should().NotBeNull();
        sentRequest!.RequestUri.Should().Be(new Uri("https://authority.guardhouse.test/api/v1/users/15"));
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldBuildExpectedQueryStringAndDeserializeResponse()
    {
        HttpRequestMessage? sentRequest = null;

        const string json = """
                            {
                              "id": 7,
                              "firstName": "John",
                              "lastName": "Doe",
                              "email": "john+test@example.com",
                              "roles": [
                                {
                                  "id": 1,
                                  "key": "system.admin",
                                  "name": "System Admin",
                                  "description": "Full access"
                                }
                              ],
                              "permissions": [
                                {
                                  "id": 2,
                                  "key": "users.read",
                                  "name": "Read Users",
                                  "description": "Can read users"
                                }
                              ],
                              "lastLogin": "2026-04-22T10:15:30Z",
                              "status": "active",
                              "isSuspended": false,
                              "isLocked": false,
                              "avatarUrl": "https://cdn.guardhouse.test/avatar.png"
                            }
                            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = CreateUsersClient();
        var response = await client.GetUserByEmailAsync("john+test@example.com");

        response.Should().NotBeNull();
        response!.Id.Should().Be(7);
        response.Email.Should().Be("john+test@example.com");
        response.Roles.Should().ContainSingle().Which.Key.Should().Be("system.admin");
        response.Permissions.Should().ContainSingle().Which.Key.Should().Be("users.read");
        response.LastLogin.Should().Be(Instant.FromUtc(2026, 4, 22, 10, 15, 30));
        response.Status.Should().Be(UserStatus.Active);
        response.IsSuspended.Should().BeFalse();
        response.IsLocked.Should().BeFalse();
        response.AvatarUrl.Should().Be("https://cdn.guardhouse.test/avatar.png");

        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Get);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users/by-email?email=john%2Btest%40example.com"));
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = CreateUsersClient();
        var response = await client.GetUserByEmailAsync("missing@example.com");

        response.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserPersonalDataAsync_ShouldSendExpectedContract()
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
                StatusCode = HttpStatusCode.NoContent
            });

        var client = CreateUsersClient();
        var result = await client.DeleteUserPersonalDataAsync(42, new DeleteUserPersonalDataRequest
        {
            TriggerWebhook = true,
            NotifyUserViaEmail = false
        });

        result.Should().BeTrue();
        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Put);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/users/42/personal-data"));

        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("triggerWebhook").GetBoolean().Should().BeTrue();
        bodyDocument.RootElement.GetProperty("notifyUserViaEmail").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateRoleAsync_ShouldSendExpectedContract()
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
                Content = new StringContent("{\"roleId\":9}")
            });

        var client = CreateRolesClient();
        var response = await client.CreateRoleAsync(new CreateRoleRequest
        {
            Key = "system.administrator",
            Name = "System Administrator",
            Description = "Full access"
        });

        response.RoleId.Should().Be(9);
        sentRequest.Should().NotBeNull();
        sentRequest!.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri.Should().Be(new Uri("https://api.guardhouse.test/api/v1/roles"));

        requestBody.Should().NotBeNull();
        using var bodyDocument = JsonDocument.Parse(requestBody!);
        bodyDocument.RootElement.GetProperty("key").GetString().Should().Be("system.administrator");
        bodyDocument.RootElement.GetProperty("name").GetString().Should().Be("System Administrator");
        bodyDocument.RootElement.GetProperty("description").GetString().Should().Be("Full access");
    }

    [Fact]
    public async Task GetPermissionByIdAsync_WhenPermissionNotFound_ShouldReturnNull()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var client = CreatePermissionsClient();
        var result = await client.GetPermissionByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePermissionAsync_WhenServerFails_ShouldExposeOnlySafeErrorCode()
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\":\"server_error\",\"detail\":\"permission=system.secret\"}")
            });

        var client = CreatePermissionsClient();

        Func<Task> act = () => client.UpdatePermissionAsync(42, new UpdatePermissionRequest
        {
            Key = "system.read",
            Name = "System Read",
            Description = "Read access",
            RoleIds = [1, 2]
        });

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("InternalServerError");
        exception.Which.Message.Should().Contain("500");
        exception.Which.Message.Should().Contain("server_error");
        exception.Which.Message.Should().NotContain("detail");
        exception.Which.Message.Should().NotContain("system.secret");
    }

    [Fact]
    public void CreateUsersClient_WhenRequireHttpsIsTrueAndApiBaseUrlIsHttp_ShouldThrow()
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

        Action act = () => _ = CreateUsersClient(insecureUserOptions, strictClientOptions);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HTTPS is required for Guardhouse API endpoint*");
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
            Mock.Of<ILogger<GuardhouseUsersClient>>());
    }

    private GuardhouseRolesClient CreateRolesClient(
        IOptions<GuardhouseUserOptions>? userOptions = null,
        IOptions<GuardhouseClientOptions>? clientOptions = null)
    {
        return new GuardhouseRolesClient(
            _httpClient,
            _mockTokenService.Object,
            userOptions ?? _userOptions,
            clientOptions ?? _clientOptions,
            Mock.Of<ILogger<GuardhouseRolesClient>>());
    }

    private GuardhousePermissionsClient CreatePermissionsClient(
        IOptions<GuardhouseUserOptions>? userOptions = null,
        IOptions<GuardhouseClientOptions>? clientOptions = null)
    {
        return new GuardhousePermissionsClient(
            _httpClient,
            _mockTokenService.Object,
            userOptions ?? _userOptions,
            clientOptions ?? _clientOptions,
            Mock.Of<ILogger<GuardhousePermissionsClient>>());
    }
}
