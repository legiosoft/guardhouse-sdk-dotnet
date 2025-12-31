using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

/// <summary>
/// Unit tests for configuration options
/// </summary>
public class OptionsTests
{
    [Fact]
    public void GuardhouseClientOptions_Validation_ShouldFailWithoutRequiredFields()
    {
        var options = new GuardhouseClientOptions();

        // Missing required fields
        var validationResults = new List<ValidationResult>();

        options.Authority = string.Empty;
        options.ClientId = string.Empty;
        options.ClientSecret = string.Empty;

        // Should fail validation
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), validationResults);

        isValid.Should().BeFalse();
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
            MaxRetryAttempts = 3
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), validationResults);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void GuardhouseResourceOptions_WithIntrospection_ShouldNotRequireClientCredentialsByDefault()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test-guardhouse.com",
            Audience = "test-audience",
            EnableIntrospection = true,
            IntrospectionClientId = string.Empty,
            IntrospectionClientSecret = "test-secret"
        };

        var validationResults = new List<ValidationResult>();

        // Should pass - no conditional validation attributes
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), validationResults);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void GuardhouseResourceOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new GuardhouseResourceOptions();

        options.Authority.Should().Be(string.Empty);
        options.Audience.Should().BeNull();
        options.EnableIntrospection.Should().BeFalse();
        options.PolicyName.Should().Be("Guardhouse");
        options.ValidateIssuer.Should().BeTrue();
        options.ValidateAudience.Should().BeTrue();
        options.ValidateLifetime.Should().BeTrue();
        options.ClockSkew.Should().Be(TimeSpan.FromMinutes(5));
    }
}