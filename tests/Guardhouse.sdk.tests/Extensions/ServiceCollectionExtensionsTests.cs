using FluentAssertions;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

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
            options.EnableIntrospection = false;
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
            options.EnableIntrospection = true;
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
    public void AddGuardhouseResource_WithIntrospectionMode_MissingClientId_ShouldThrowValidationException()
    {
        var services = new ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = string.Empty;
            options.IntrospectionClientSecret = "test-secret";
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*IntrospectionClientId is required*");
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionMode_MissingClientSecret_ShouldThrowValidationException()
    {
        var services = new ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "test-client";
            options.IntrospectionClientSecret = string.Empty;
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*IntrospectionClientSecret is required*");
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
    }

    [Fact]
    public void AddGuardhouseResource_MissingAuthority_ShouldThrowValidationException()
    {
        var services = new ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = string.Empty;
            options.Audience = "test-audience";
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*Authority is required*");
    }

    [Fact]
    public void AddGuardhouseResource_MissingAudience_ShouldThrowValidationException()
    {
        var services = new ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = string.Empty;
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*Audience is required*");
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
