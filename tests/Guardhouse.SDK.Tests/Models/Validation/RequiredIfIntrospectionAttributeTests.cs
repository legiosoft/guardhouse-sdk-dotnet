using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Validation;
using Xunit;

namespace Guardhouse.SDK.Tests.Models.Validation;

public class RequiredIfIntrospectionAttributeTests
{
    [Fact]
    public void IsValid_WithIntrospectionModeAndNullValue_ShouldReturnValidationError()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = null
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
        result.ErrorMessage.Should().Contain("Introspection");
    }

    [Fact]
    public void IsValid_WithIntrospectionModeAndEmptyString_ShouldReturnValidationError()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = string.Empty
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
        result.ErrorMessage.Should().Contain("Introspection");
    }

    [Fact]
    public void IsValid_WithIntrospectionModeAndWhitespace_ShouldReturnValidationError()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "   "
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
        result.ErrorMessage.Should().Contain("Introspection");
    }

    [Fact]
    public void IsValid_WithIntrospectionModeAndValidValue_ShouldReturnSuccess()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientId = "test-client"
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_WithJwtSignatureModeAndNullValue_ShouldReturnSuccess()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.JwtSignature,
            IntrospectionClientId = null
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_WithJwtSignatureModeAndEmptyString_ShouldReturnSuccess()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.JwtSignature,
            IntrospectionClientId = string.Empty
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientId, validationContext);

        result.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_WithCustomErrorMessage_ShouldUseCustomMessage()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientSecret = null
        };

        var attribute = new RequiredIfIntrospectionAttribute
        {
            ErrorMessage = "Custom error message"
        };
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientSecret, validationContext);

        result.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.Should().Be("Custom error message");
    }

    [Fact]
    public void IsValid_WhenValidatingIntrospectionClientSecret_ShouldWorkCorrectly()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientSecret = "test-secret"
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientSecret, validationContext);

        result.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void IsValid_WhenIntrospectionClientSecretIsMissing_ShouldReturnValidationError()
    {
        var options = new GuardhouseResourceOptions
        {
            Authority = "https://test.com",
            Audience = "test-audience",
            ValidationMode = TokenValidationMode.Introspection,
            IntrospectionClientSecret = null
        };

        var attribute = new RequiredIfIntrospectionAttribute();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var result = attribute.GetValidationResult(options.IntrospectionClientSecret, validationContext);

        result.Should().NotBe(System.ComponentModel.DataAnnotations.ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("required");
        result.ErrorMessage.Should().Contain("Introspection");
    }
}
