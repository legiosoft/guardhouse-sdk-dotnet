using System;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

/// <summary>
/// Unit tests for DI extensions
/// </summary>
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
        services.Should().Contain(sd => sd.ServiceType == typeof(GuardhouseTokenService));
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
        services.Should().Contain(sd => sd.ServiceType == typeof(GuardhouseResourceService));
        services.Should().Contain(sd => sd.ServiceType == typeof(GuardhouseJwtBearerEvents));
        services.Should().Contain(sd => sd.ServiceType == typeof(IConfigureOptions<JwtBearerOptions>));
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospection_ShouldConfigureCorrectly()
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
    public void AddGuardhouse_WithBothConfigurations_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouse(
            clientOptions: co => {
                co.Authority = "https://test.com";
                co.ClientId = "test-client";
                co.ClientSecret = "test-secret";
            },
            resourceOptions: ro => {
                ro.Authority = "https://test.com";
                ro.Audience = "test-audience";
                ro.EnableIntrospection = true;
            });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseResourceService));
    }

    [Fact]
    public void ConfigureGuardhouseResilience_ShouldRegisterOptions()
    {
        var services = new ServiceCollection();
        services.ConfigureGuardhouseResilience(options =>
        {
            options.MaxRetryAttempts = 5;
            options.RequestTimeoutSeconds = 60;
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<GuardhouseResilienceOptions>().Value;

        options.MaxRetryAttempts.Should().Be(5);
        options.RequestTimeoutSeconds.Should().Be(60);
    }
}