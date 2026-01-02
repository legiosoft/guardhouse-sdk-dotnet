using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication;
using Moq;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseResourceServiceTests
{
    private readonly Mock<IAuthenticationSchemeProvider> _mockSchemeProvider;
    private readonly GuardhouseResourceService _service;

    public GuardhouseResourceServiceTests()
    {
        _mockSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
        _service = new GuardhouseResourceService(_mockSchemeProvider.Object);
    }

    #region ValidateTokenAsync Tests

    [Fact]
    public async Task ValidateTokenAsync_ShouldThrowInvalidOperationException()
    {
        var token = "test_token";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ValidateTokenAsync(token));

        exception.Message.Should().Contain("HttpContext context");
        exception.Message.Should().Contain("JWT bearer authentication");
    }

    #endregion

    #region IntrospectTokenAsync Tests

    [Fact]
    public async Task IntrospectTokenAsync_ShouldThrowInvalidOperationException()
    {
        var token = "test_token";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.IntrospectTokenAsync(token));

        exception.Message.Should().Contain("authentication pipeline");
        exception.Message.Should().Contain("IGuardhouseIntrospectionService");
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
}
