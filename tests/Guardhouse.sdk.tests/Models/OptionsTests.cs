using FluentAssertions;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class OptionsTests
{
    [Fact]
    public void GuardhouseClientOptions_Validation_ShouldFailWithoutRequiredFields()
    {
        var options = new GuardhouseClientOptions();

        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, new System.ComponentModel.DataAnnotations.ValidationContext(options), validationResults);

        isValid.Should().BeFalse();
        validationResults.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public void GuardhouseClientOptions_WithValidFields_ShouldPassValidation()
    {
        var options = new GuardhouseClientOptions
        {
            Authority = "https://test-guardhouse.com",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Scope = "api read write",
            EnableTokenCaching = true,
            EnableTokenRefresh = true,
            CacheExpirationBufferSeconds = 60,
            RequestTimeoutSeconds = 30,
            MaxRetryAttempts = 3,
            EnableHttpResilience = true
        };

        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, new System.ComponentModel.DataAnnotations.ValidationContext(options), validationResults);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void GuardhouseClientOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new GuardhouseClientOptions();

        options.Authority.Should().Be(string.Empty);
        options.ClientId.Should().Be(string.Empty);
        options.ClientSecret.Should().Be(string.Empty);
        options.Scope.Should().Be("api");
        options.EnableTokenCaching.Should().BeTrue();
        options.EnableTokenRefresh.Should().BeTrue();
        options.CacheExpirationBufferSeconds.Should().Be(60);
        options.RequestTimeoutSeconds.Should().Be(30);
        options.MaxRetryAttempts.Should().Be(3);
        options.EnableHttpResilience.Should().BeTrue();
    }

    [Fact]
    public void GuardhouseResourceOptions_WithIntrospectionMode_MissingClientId_ShouldFailValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = string.Empty;
            options.IntrospectionClientSecret = "test-secret";
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*IntrospectionClientId is required*");
    }

    [Fact]
    public void GuardhouseResourceOptions_WithIntrospectionMode_MissingClientSecret_ShouldFailValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "test-client";
            options.IntrospectionClientSecret = string.Empty;
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*IntrospectionClientSecret is required*");
    }

    [Fact]
    public void GuardhouseResourceOptions_WithIntrospectionMode_WithValidCredentials_ShouldPassValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "test-client";
            options.IntrospectionClientSecret = "test-secret";
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void GuardhouseResourceOptions_WithJwtSignatureMode_ShouldNotRequireIntrospectionCredentials()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.JwtSignature;
            options.IntrospectionClientId = string.Empty;
            options.IntrospectionClientSecret = string.Empty;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void GuardhouseResourceOptions_MissingAuthority_ShouldFailValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = string.Empty;
            options.Audience = "test-audience";
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*Authority is required*");
    }

    [Fact]
    public void GuardhouseResourceOptions_MissingAudience_ShouldFailValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = string.Empty;
        });

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>()
            .WithMessage("*Audience is required*");
    }

    [Fact]
    public void GuardhouseResourceOptions_WithValidJwtSignatureMode_ShouldPassValidation()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test-guardhouse.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.JwtSignature;
            options.ValidAlgorithms = new[] { "RS256" };
            options.TokenTypes = new[] { "JWT" };
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void GuardhouseResourceOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new GuardhouseResourceOptions();

        options.Authority.Should().Be(string.Empty);
        options.Audience.Should().Be("my_resource_api");
        options.ValidationMode.Should().Be(TokenValidationMode.JwtSignature);
        options.EnableIntrospection.Should().BeFalse();
        options.PolicyName.Should().Be("Guardhouse");
        options.ValidateIssuer.Should().BeTrue();
        options.ValidateAudience.Should().BeTrue();
        options.ValidateLifetime.Should().BeTrue();
        options.ValidateIssuerSigningKey.Should().BeTrue();
        options.RequireHttpsMetadata.Should().BeNull();
        options.JwksCacheDurationHours.Should().Be(24);
        options.JwksRefreshIntervalMinutes.Should().Be(5);
        options.ValidAlgorithms.Should().Contain("RS256");
        options.TokenTypes.Should().Contain("JWT");
    }
}
