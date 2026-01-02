using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class GuardhouseJwtBearerEventsTests
{
    private readonly Mock<IOptions<GuardhouseResourceOptions>> _mockOptions;
    private readonly Mock<IGuardhouseIntrospectionService> _mockIntrospectionService;
    private readonly GuardhouseIntrospectionJwtBearerEvents _events;

    public GuardhouseJwtBearerEventsTests()
    {
        _mockOptions = new Mock<IOptions<GuardhouseResourceOptions>>();
        _mockIntrospectionService = new Mock<IGuardhouseIntrospectionService>();
        _mockOptions.Setup(x => x.Value).Returns(new GuardhouseResourceOptions
        {
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        });
        _events = new GuardhouseIntrospectionJwtBearerEvents(_mockOptions.Object, _mockIntrospectionService.Object);
    }

    #region Introspection Mode Tests

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_ActiveToken_ShouldSucceed()
    {
        var context = CreateTokenValidatedContext(token: "valid_token");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user123",
                Username = "testuser",
                Scope = "read write"
            });

        await _events.TokenValidated(context);

        context.Principal.Should().NotBeNull();
        context.Principal?.Identity?.IsAuthenticated.Should().BeTrue();
        context.Principal?.FindFirst("sub")?.Value.Should().Be("user123");
        context.Principal?.FindFirst(ClaimTypes.Name)?.Value.Should().Be("testuser");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_InactiveToken_ShouldFail()
    {
        var context = CreateTokenValidatedContext(token: "inactive_token");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("inactive_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = false
            });

        await _events.TokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result?.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_Error_ShouldFail()
    {
        var context = CreateTokenValidatedContext(token: "error_token");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("error_token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Introspection service error"));

        await _events.TokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result?.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_ReplacesPrincipal()
    {
        var context = CreateTokenValidatedContext(token: "token123");
        
        var originalPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] 
        { 
            new Claim("old_claim", "old_value") 
        }, "Original"));
        context.Principal = originalPrincipal;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "new_user",
                Scope = "api"
            });

        await _events.TokenValidated(context);

        context.Principal.Should().NotBe(originalPrincipal);
        context.Principal?.FindFirst("old_claim").Should().BeNull();
        context.Principal?.FindFirst("sub")?.Value.Should().Be("new_user");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_ParsesScopeCorrectly()
    {
        var context = CreateTokenValidatedContext(token: "token_with_scopes");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_scopes", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Scope = "read write delete  admin"
            });

        await _events.TokenValidated(context);

        var scopes = context.Principal?.FindAll("scope").Select(c => c.Value).ToList();
        scopes.Should().HaveCount(4);
        scopes.Should().Contain(new[] { "read", "write", "delete", "admin" });
    }

    [Fact]
    public async Task TokenValidated_ExtractsTokenFromSecurityToken()
    {
        var token = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.invalid";
        var context = CreateTokenValidatedContext(token: token);

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse { Active = true });

        await _events.TokenValidated(context);

        _mockIntrospectionService.Verify(x => x.IntrospectTokenAsync(token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TokenValidated_WithInvalidTokenType_ShouldFail()
    {
        var context = CreateTokenValidatedContext(token: "some_token");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("some_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                TokenType = "Bearer",
                Algorithm = "RS256"
            });

        await _events.TokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result?.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_WithInvalidAlgorithm_ShouldFail()
    {
        var context = CreateTokenValidatedContext(token: "some_token");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("some_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                TokenType = "JWT",
                Algorithm = "HS256"
            });

        await _events.TokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result?.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_MapsSingleRole()
    {
        var context = CreateTokenValidatedContext(token: "token_with_role");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_role", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Role = "Admin"
            });

        await _events.TokenValidated(context);

        context.Principal?.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public async Task TokenValidated_MapsMultipleRoles()
    {
        var context = CreateTokenValidatedContext(token: "token_with_roles");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_roles", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Roles = "Admin Editor Viewer"
            });

        await _events.TokenValidated(context);

        var principal = context.Principal;
        principal.Should().NotBeNull();
        principal?.IsInRole("Admin").Should().BeTrue();
        principal?.IsInRole("Editor").Should().BeTrue();
        principal?.IsInRole("Viewer").Should().BeTrue();
    }

    [Fact]
    public async Task TokenValidated_PreventsDuplicateRoles()
    {
        var context = CreateTokenValidatedContext(token: "token_with_duplicate_roles");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_duplicate_roles", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Role = "Admin",
                Roles = "Admin Editor Admin Viewer"
            });

        await _events.TokenValidated(context);

        var principal = context.Principal;
        principal.Should().NotBeNull();
        var roleClaims = principal?.FindAll(ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(3);
        roleClaims.Should().OnlyContain(c => new[] { "Admin", "Editor", "Viewer" }.Contains(c.Value));
    }

    [Fact]
    public async Task TokenValidated_MapsSubjectToNameIdentifier()
    {
        var context = CreateTokenValidatedContext(token: "token_with_sub");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_sub", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user123"
            });

        await _events.TokenValidated(context);

        var principal = context.Principal;
        principal.Should().NotBeNull();
        principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user123");
        principal?.FindFirst(GuardhouseConstants.JwtClaims.Subject)?.Value.Should().Be("user123");
    }

    [Fact]
    public async Task TokenValidated_MapsMultipleAudiences()
    {
        var context = CreateTokenValidatedContext(token: "token_with_audiences");

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("token_with_audiences", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Aud = "api1 api2 api3"
            });

        await _events.TokenValidated(context);

        var principal = context.Principal;
        principal.Should().NotBeNull();
        var audienceClaims = principal?.FindAll(GuardhouseConstants.JwtClaims.Audience).ToList();
        audienceClaims.Should().HaveCount(3);
        audienceClaims.Should().OnlyContain(c => new[] { "api1", "api2", "api3" }.Contains(c.Value));
    }

    #endregion

    private static TokenValidatedContext CreateTokenValidatedContext(string token)
    {
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        var jwtToken = new JwtSecurityToken("https://test.com", "api", null, null, DateTime.UtcNow + TimeSpan.FromHours(1), new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("test_key_test_key_test_key_test_key_test_key_test_key_test_key_test_key_test_key_")),
            "HS256"));

        var tokenProp = jwtToken.GetType().GetProperty("RawData");
        tokenProp?.SetValue(jwtToken, token);

        var options = new JwtBearerOptions
        {
            TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuer = "https://test.com",
                ValidAudience = "api"
            }
        };

        var scheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme("Bearer", "Bearer", typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity()),
            SecurityToken = jwtToken
        };

        return context;
    }
}
