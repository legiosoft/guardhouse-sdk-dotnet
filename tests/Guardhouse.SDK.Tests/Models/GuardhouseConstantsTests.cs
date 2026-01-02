using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class GuardhouseConstantsTests
{
    [Fact]
    public void Endpoints_WellKnownOpenIdConfiguration_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Endpoints.WellKnownOpenIdConfiguration.Should().Be(".well-known/openid-configuration");
    }

    [Fact]
    public void Endpoints_WellKnownJwks_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Endpoints.WellKnownJwks.Should().Be(".well-known/jwks.json");
    }

    [Fact]
    public void Endpoints_ConnectToken_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Endpoints.ConnectToken.Should().Be("connect/token");
    }

    [Fact]
    public void Endpoints_ConnectIntrospect_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Endpoints.ConnectIntrospect.Should().Be("connect/introspect");
    }

    [Fact]
    public void Algorithms_RS256_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Algorithms.RS256.Should().Be("RS256");
    }

    [Fact]
    public void Algorithms_None_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Algorithms.None.Should().Be("none");
    }

    [Fact]
    public void TokenTypes_Jwt_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.TokenTypes.Jwt.Should().Be("JWT");
    }

    [Fact]
    public void Headers_Authorization_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Headers.Authorization.Should().Be("Authorization");
    }

    [Fact]
    public void Headers_BearerPrefix_ShouldBeCorrect()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Headers.BearerPrefix.Should().Be("Bearer ");
    }

    [Fact]
    public void Defaults_JwksCacheDurationHours_ShouldBe24()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.JwksCacheDurationHours.Should().Be(24);
    }

    [Fact]
    public void Defaults_JwksRefreshIntervalMinutes_ShouldBe5()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes.Should().Be(5);
    }

    [Fact]
    public void Defaults_CacheExpirationBufferSeconds_ShouldBe60()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.CacheExpirationBufferSeconds.Should().Be(60);
    }

    [Fact]
    public void Defaults_RequestTimeoutSeconds_ShouldBe30()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.RequestTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Defaults_MaxRetryAttempts_ShouldBe3()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.MaxRetryAttempts.Should().Be(3);
    }

    [Fact]
    public void Defaults_DefaultScope_ShouldBeApi()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.DefaultScope.Should().Be("api");
    }

    [Fact]
    public void Defaults_ClockSkewMinutes_ShouldBe5()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Defaults.ClockSkewMinutes.Should().Be(5.0);
    }

    [Fact]
    public void Validation_ValidateIssuer_ShouldBeTrue()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Validation.ValidateIssuer.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateAudience_ShouldBeTrue()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Validation.ValidateAudience.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateLifetime_ShouldBeTrue()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Validation.ValidateLifetime.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateIssuerSigningKey_ShouldBeTrue()
    {
        Guardhouse.SDK.Constants.GuardhouseConstants.Validation.ValidateIssuerSigningKey.Should().BeTrue();
    }
}
