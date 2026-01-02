using FluentAssertions;
using Guardhouse.SDK.Models;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class TokenValidationModeTests
{
    [Fact]
    public void JwtSignature_ShouldHaveCorrectValue()
    {
        TokenValidationMode.JwtSignature.Should().Be(0);
    }

    [Fact]
    public void Introspection_ShouldHaveCorrectValue()
    {
        TokenValidationMode.Introspection.Should().Be(1);
    }

    [Fact]
    public void Default_ShouldBeJwtSignature()
    {
        var options = new GuardhouseResourceOptions();
        options.ValidationMode.Should().Be(TokenValidationMode.JwtSignature);
    }
}
