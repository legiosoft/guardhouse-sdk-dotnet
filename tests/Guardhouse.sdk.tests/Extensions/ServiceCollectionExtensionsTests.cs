using FluentAssertions;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

using Microsoft.Extensions.Caching.Memory;
using SDK.Services;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGuardhouseClient_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.Scope = "api";
        });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IMemoryCache));
    }

    [Fact]
    public void AddGuardhouseClient_WithMinimalParameters_ShouldConfigureOptions()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(
            authority: "https://test.com",
            clientId: "test-client",
            clientSecret: "test-secret",
            scope: "api");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;

        options.Authority.Should().Be("https://test.com");
        options.ClientId.Should().Be("test-client");
        options.ClientSecret.Should().Be("test-secret");
        options.Scope.Should().Be("api");
    }

    [Fact]
    public void AddGuardhouseResource_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
        });

        services.Should().Contain(sd => sd.ServiceType == typeof(IAuthenticationService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseResourceService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseIntrospectionService));
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionMode_ShouldConfigureCorrectly()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "introspection-client";
            options.IntrospectionClientSecret = "introspection-secret";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.EnableIntrospection.Should().BeTrue();
        options.IntrospectionClientId.Should().Be("introspection-client");
        options.IntrospectionClientSecret.Should().Be("introspection-secret");
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionMode_ShouldRequireIntrospectionCredentials()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "introspection-client";
            options.IntrospectionClientSecret = "introspection-secret";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.IntrospectionClientId.Should().Be("introspection-client");
        options.IntrospectionClientSecret.Should().Be("introspection-secret");
    }

    [Fact]
    public void AddGuardhouseResource_WithJwtSignatureMode_ShouldNotRequireIntrospectionCredentials()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.JwtSignature;
        });

        act.Should().NotThrow();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.EnableIntrospection.Should().BeFalse();
        options.IntrospectionClientId.Should().BeNull();
        options.IntrospectionClientSecret.Should().BeNull();
    }

    [Fact]
    public void AddGuardhouse_WithBothConfigurations_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouse(
            configureClientAction: co => {
                co.Authority = "https://test.com";
                co.ClientId = "test-client";
                co.ClientSecret = "test-secret";
            },
            configureResourceAction: ro => {
                ro.Authority = "https://test.com";
                ro.Audience = "test-audience";
            });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseResourceService));
    }
}
