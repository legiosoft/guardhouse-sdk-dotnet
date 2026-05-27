using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseResourceServiceTests
{
    private readonly Mock<IAuthenticationSchemeProvider> _mockSchemeProvider;
    private readonly Mock<IGuardhouseIntrospectionService> _mockIntrospectionService;
    private readonly Mock<IOptionsMonitor<JwtBearerOptions>> _mockJwtBearerOptions;
    private readonly GuardhouseResourceService _service;

    public GuardhouseResourceServiceTests()
    {
        _mockSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
        _mockIntrospectionService = new Mock<IGuardhouseIntrospectionService>();
        _mockJwtBearerOptions = new Mock<IOptionsMonitor<JwtBearerOptions>>();

        _service = CreateService(new GuardhouseResourceOptions
        {
            Authority = "https://auth.example.com",
            Audience = "api",
            ValidationMode = TokenValidationMode.Introspection
        });
    }

    private GuardhouseResourceService CreateService(
        GuardhouseResourceOptions options,
        ILogger<GuardhouseResourceService>? logger = null)
    {
        var jwtOptions = new JwtBearerOptions
        {
            MapInboundClaims = false,
            TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [GuardhouseConstants.Algorithms.RS256],
                NameClaimType = "name",
                RoleClaimType = "role"
            }
        };

        _mockJwtBearerOptions
            .Setup(x => x.Get(options.PolicyName))
            .Returns(jwtOptions);

        return new GuardhouseResourceService(
            _mockIntrospectionService.Object,
            _mockSchemeProvider.Object,
            Options.Create(options),
            _mockJwtBearerOptions.Object,
            logger);
    }

    #region ValidateTokenAsync Tests

    [Fact]
    public async Task ValidateTokenAsync_WithIntrospectionMode_ShouldReturnPrincipal()
    {
        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("test_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                Scope = "api"
            });

        var principal = await _service.ValidateTokenAsync("test_token");

        principal.Should().NotBeNull();
        principal!.FindFirst(GuardhouseConstants.JwtClaims.Subject)?.Value.Should().Be("user");
    }

    [Fact]
    public async Task ValidateTokenAsync_WithCustomIntrospectionClaims_ShouldMapToPrincipal()
    {
        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("test_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = true,
                Sub = "user",
                AdditionalClaims = new Dictionary<string, JsonElement>
                {
                    ["system"] = ParseJsonElement("\"system_administrator\""),
                    ["business"] = ParseJsonElement("[\"retail\",\"wholesale\"]")
                }
            });

        var principal = await _service.ValidateTokenAsync("test_token");

        principal.Should().NotBeNull();
        principal!.FindFirst("system")?.Value.Should().Be("system_administrator");
        principal.FindAll("business").Select(claim => claim.Value)
            .Should().BeEquivalentTo("retail", "wholesale");
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInactiveToken_ShouldReturnNull()
    {
        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("test_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = false
            });

        var principal = await _service.ValidateTokenAsync("test_token");

        principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInactiveToken_ShouldNotLogWarning()
    {
        var logger = new Mock<ILogger<GuardhouseResourceService>>();
        var service = CreateService(new GuardhouseResourceOptions
        {
            Authority = "https://auth.example.com",
            Audience = "api",
            ValidationMode = TokenValidationMode.Introspection
        }, logger.Object);

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("test_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = false
            });

        var principal = await service.ValidateTokenAsync("test_token");

        principal.Should().BeNull();
        VerifyNoWarningLog(logger);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenIntrospectionFails_ShouldReturnNull()
    {
        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync("test_token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("network error"));

        var principal = await _service.ValidateTokenAsync("test_token");

        principal.Should().BeNull();
    }

    #endregion

    #region IntrospectTokenAsync Tests

    [Fact]
    public async Task IntrospectTokenAsync_ShouldProxyService()
    {
        var token = "test_token";
        var expectedResponse = new IntrospectionResponse { Active = true };

        _mockIntrospectionService
            .Setup(x => x.IntrospectTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var response = await _service.IntrospectTokenAsync(token);

        response.Should().BeSameAs(expectedResponse);
    }

    #endregion

    #region GetDefaultSchemeAsync Tests

    [Fact]
    public async Task GetDefaultSchemeAsync_WhenSchemeExists_ShouldReturnScheme()
    {
        var expectedScheme = new AuthenticationScheme(
            "Bearer",
            "Bearer",
            typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler));

        _mockSchemeProvider
            .Setup(x => x.GetDefaultAuthenticateSchemeAsync())
            .ReturnsAsync(expectedScheme);

        var result = await _service.GetDefaultSchemeAsync();

        result.Should().NotBeNull();
        result!.Name.Should().Be("Bearer");
        result.DisplayName.Should().Be("Bearer");
    }

    [Fact]
    public async Task GetDefaultSchemeAsync_WhenSchemeDoesNotExist_ShouldReturnNull()
    {
        _mockSchemeProvider
            .Setup(x => x.GetDefaultAuthenticateSchemeAsync())
            .ReturnsAsync((AuthenticationScheme?)null);

        var result = await _service.GetDefaultSchemeAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultSchemeAsync_ShouldCallProviderOnce()
    {
        _mockSchemeProvider
            .Setup(x => x.GetDefaultAuthenticateSchemeAsync())
            .ReturnsAsync((AuthenticationScheme?)null);

        await _service.GetDefaultSchemeAsync();

        _mockSchemeProvider.Verify(
            x => x.GetDefaultAuthenticateSchemeAsync(),
            Times.Once);
    }

    #endregion

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void VerifyNoWarningLog<T>(Mock<ILogger<T>> logger)
    {
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
