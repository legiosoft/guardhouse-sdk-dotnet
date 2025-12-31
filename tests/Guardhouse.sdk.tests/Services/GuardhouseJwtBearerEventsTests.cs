using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

/// <summary>
/// Unit tests for GuardhouseJwtBearerEvents
/// </summary>
public class GuardhouseJwtBearerEventsTests
{
    private readonly Mock<IOptions<GuardhouseResourceOptions>> _mockOptions;
    private readonly Mock<IGuardhouseTokenService> _mockTokenService;
    private readonly Mock<ILogger<GuardhouseJwtBearerEvents>> _mockLogger;

    public GuardhouseJwtBearerEventsTests()
    {
        _mockOptions = new Mock<IOptions<GuardhouseResourceOptions>>();
        _mockTokenService = new Mock<IGuardhouseTokenService>();
        _mockLogger = new Mock<ILogger<GuardhouseJwtBearerEvents>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldUseNullLogger()
    {
        var options = Options.Create(new GuardhouseResourceOptions());
        var events = new GuardhouseJwtBearerEvents(options);

        events.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTokenService_ShouldUseProvidedService()
    {
        var options = Options.Create(new GuardhouseResourceOptions { EnableIntrospection = true });
        var tokenServiceMock = new Mock<IGuardhouseTokenService>();

        var events = new GuardhouseJwtBearerEvents(options, tokenServiceMock.Object);

        events.Should().NotBeNull();
    }

    [Fact]
    public async Task TokenValidated_WithoutIntrospection_ShouldSucceed()
    {
        var options = Options.Create(new GuardhouseResourceOptions { EnableIntrospection = false });
        var context = CreateTokenValidatedContext(options.Value);

        var events = new GuardhouseJwtBearerEvents(options, tokenService: null, _mockLogger.Object);

        await events.Invoking(e => e.TokenValidated(context)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenValidated_WithIntrospection_AndActiveToken_ShouldAddClaims()
    {
        var options = Options.Create(new GuardhouseResourceOptions
        {
            EnableIntrospection = true,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret"
        });

        var introspectionResponse = new IntrospectionResponse
        {
            Active = true,
            ClientId = "test-client",
            Username = "testuser",
            Scope = "api read write",
            Sub = "user123",
            Aud = "test-audience"
        };

        var tokenServiceMock = new Mock<IGuardhouseTokenService>();
        tokenServiceMock
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(introspectionResponse);

        var context = CreateTokenValidatedContext(options.Value, "Bearer test_token");

        var events = new GuardhouseJwtBearerEvents(options, tokenServiceMock.Object, _mockLogger.Object);
        await events.TokenValidated(context);

        var principal = context.Principal;
        principal.Should().NotBeNull();

        var claims = principal!.Claims;
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        claims.Should().Contain(c => c.Type == "scope" && c.Value == "api read write");
        claims.Should().Contain(c => c.Type == "audience" && c.Value == "test-audience");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospection_AndInactiveToken_ShouldFail()
    {
        var options = Options.Create(new GuardhouseResourceOptions
        {
            EnableIntrospection = true,
            IntrospectionClientId = "test-client",
            IntrospectionClientSecret = "test-secret"
        });

        var introspectionResponse = new IntrospectionResponse { Active = false };

        var tokenServiceMock = new Mock<IGuardhouseTokenService>();
        tokenServiceMock
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(introspectionResponse);

        var context = CreateTokenValidatedContext(options.Value, "Bearer test_token");

        var events = new GuardhouseJwtBearerEvents(options, tokenServiceMock.Object, _mockLogger.Object);
        await events.TokenValidated(context);

        context.Result.Should().NotBeNull();
        context.Result.Succeeded.Should().BeFalse();
    }

    private static TokenValidatedContext CreateTokenValidatedContext(GuardhouseResourceOptions options, string? token = null)
    {
        var httpContext = new DefaultHttpContext();
        if (token != null)
        {
            httpContext.Request.Headers.Authorization = token;
        }

        var scheme = new AuthenticationScheme("TestScheme", "TestScheme", typeof(AuthenticationMiddleware));
        var jwtOptions = new JwtBearerOptions();

        return new TokenValidatedContext(httpContext, scheme, jwtOptions);
    }
}