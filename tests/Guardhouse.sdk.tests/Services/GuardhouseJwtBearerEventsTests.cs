using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseJwtBearerEventsTests
{
    private readonly Mock<IOptions<GuardhouseResourceOptions>> _mockOptions;
    private readonly Mock<IGuardhouseIntrospectionService> _mockIntrospectionService;
    private readonly GuardhouseIntrospectionJwtBearerEvents _events;

    public GuardhouseJwtBearerEventsTests()
    {
        _mockOptions = new Mock<IOptions<GuardhouseResourceOptions>>();
        _mockIntrospectionService = new Mock<IGuardhouseIntrospectionService>();
        _events = new GuardhouseIntrospectionJwtBearerEvents(
            _mockOptions.Object,
            _mockIntrospectionService.Object);
    }

    #region JWT Signature Mode Tests

    [Fact]
    public async Task TokenValidated_WithJwtSignatureMode_ValidToken_ShouldSucceed()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.JwtSignature,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_WithJwtSignatureMode_InvalidAlgorithm_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.JwtSignature,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken(alg: "HS256"));
        var jwtToken = CreateJwtToken(alg: "HS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Algorithm 'HS256' is not allowed");
    }

    [Fact]
    public async Task TokenValidated_WithJwtSignatureMode_InvalidTokenType_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.JwtSignature,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken(alg: "RS256", typ: "Bearer"));
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "Bearer");
        context.SecurityToken = jwtToken;

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Token type 'Bearer' is not allowed");
    }

    [Fact]
    public async Task TokenValidated_WithJwtSignatureMode_NoneAlgorithm_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.JwtSignature,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken(alg: "none", typ: "JWT"));
        var jwtToken = CreateJwtToken(alg: "none", typ: "JWT");
        context.SecurityToken = jwtToken;

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Algorithm 'none' is not allowed");
    }

    #endregion

    #region Introspection Mode Tests

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_ActiveToken_ShouldSucceed()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.Introspection,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Algorithm = "RS256",
                Signature = "valid_signature"
            });

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_InactiveToken_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.Introspection,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = false
            });

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Token is not active");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_InvalidAlgorithm_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.Introspection,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Algorithm = "HS256"
            });

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Algorithm 'HS256' is not allowed");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_InvalidTokenType_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.Introspection,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                TokenType = "Bearer"
            });

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Token type 'Bearer' is not allowed");
    }

    [Fact]
    public async Task TokenValidated_WithIntrospectionMode_MissingSignature_ShouldFail()
    {
        var options = new GuardhouseResourceOptions
        {
            ValidationMode = TokenValidationMode.Introspection,
            ValidAlgorithms = new[] { "RS256" },
            TokenTypes = new[] { "JWT" }
        };

        _mockOptions.Setup(x => x.Value).Returns(options);

        var context = CreateTokenValidatedContext(token: CreateValidJwtToken());
        var jwtToken = CreateJwtToken(alg: "RS256", typ: "JWT");
        context.SecurityToken = jwtToken;

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Signature = null
            });

        await _events.TokenValidated(context);

        context.FailCalled.Should().BeTrue();
        context.FailureMessage.Should().Be("Token signature is missing");
    }

    #endregion

    #region Helper Methods

    private static TokenValidatedContext CreateTokenValidatedContext(string token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {token}";

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(), JwtBearerDefaults.AuthenticationScheme);
        return new TokenValidatedContext(httpContext, ticket);
    }

    private static JwtSecurityToken CreateValidJwtToken(string alg = "RS256", string typ = "JWT")
    {
        var header = new JwtHeader(alg, typ);
        var payload = new JwtPayload(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
        return new JwtSecurityToken(header, payload);
    }

    private static JwtSecurityToken CreateJwtToken(string alg, string typ)
    {
        var header = new JwtHeader(alg, typ);
        var payload = new JwtPayload(new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
        return new JwtSecurityToken(header, payload);
    }

    #endregion
}
