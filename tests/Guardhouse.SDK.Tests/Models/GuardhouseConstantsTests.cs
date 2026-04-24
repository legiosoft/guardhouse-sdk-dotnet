using Xunit;

namespace Guardhouse.SDK.Tests.Models;

using FluentAssertions;

public class GuardhouseConstantsTests
{
    [Fact]
    public void Endpoints_WellKnownOpenIdConfiguration_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Endpoints.WellKnownOpenIdConfiguration.Should().Be(".well-known/openid-configuration");
    }

    [Fact]
    public void Endpoints_WellKnownJwks_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Endpoints.WellKnownJwks.Should().Be(".well-known/jwks");
    }

    [Fact]
    public void Endpoints_ConnectToken_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Endpoints.ConnectToken.Should().Be("connect/token");
    }

    [Fact]
    public void Endpoints_ConnectIntrospect_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Endpoints.ConnectIntrospect.Should().Be("connect/introspect");
    }

    [Fact]
    public void Algorithms_RS256_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Algorithms.RS256.Should().Be("RS256");
    }

    [Fact]
    public void Algorithms_None_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Algorithms.None.Should().Be("none");
    }

    [Fact]
    public void TokenTypes_Jwt_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.TokenTypes.Jwt.Should().Be("JWT");
    }

    [Fact]
    public void TokenTypes_AtJwt_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.TokenTypes.AtJwt.Should().Be("at+jwt");
    }

    [Fact]
    public void Headers_Authorization_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Headers.Authorization.Should().Be("Authorization");
    }

    [Fact]
    public void Headers_BearerPrefix_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Headers.BearerPrefix.Should().Be("Bearer ");
    }

    [Fact]
    public void Headers_WebhookSignature_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Headers.WebhookSignature.Should().Be("X-Hub-Signature");
    }

    [Fact]
    public void Scopes_OfflineAccess_ShouldBeCorrect()
    {
        Constants.GuardhouseConstants.Scopes.OfflineAccess.Should().Be("offline_access");
    }

    [Fact]
    public void Defaults_JwksCacheDurationHours_ShouldBe24()
    {
        Constants.GuardhouseConstants.Defaults.JwksCacheDurationHours.Should().Be(24);
    }

    [Fact]
    public void Defaults_JwksRefreshIntervalMinutes_ShouldBe5()
    {
        Constants.GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes.Should().Be(5);
    }

    [Fact]
    public void Defaults_CacheExpirationBufferSeconds_ShouldBe60()
    {
        Constants.GuardhouseConstants.Defaults.CacheExpirationBufferSeconds.Should().Be(60);
    }

    [Fact]
    public void Defaults_RequestTimeoutSeconds_ShouldBe30()
    {
        Constants.GuardhouseConstants.Defaults.RequestTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Defaults_MaxRetryAttempts_ShouldBe3()
    {
        Constants.GuardhouseConstants.Defaults.MaxRetryAttempts.Should().Be(3);
    }

    [Fact]
    public void Defaults_DefaultScope_ShouldBeApi()
    {
        Constants.GuardhouseConstants.Defaults.DefaultScope.Should().Be("api");
    }

    [Fact]
    public void Defaults_ClockSkewMinutes_ShouldBe5()
    {
        Constants.GuardhouseConstants.Defaults.ClockSkewMinutes.Should().Be(5.0);
    }

    [Fact]
    public void Defaults_WebhookSignatureToleranceSeconds_ShouldBe300()
    {
        Constants.GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds.Should().Be(300);
    }

    [Fact]
    public void Defaults_WebhookMaxBodySizeBytes_ShouldBe5MiB()
    {
        Constants.GuardhouseConstants.Defaults.WebhookMaxBodySizeBytes.Should().Be(5 * 1024 * 1024);
    }

    [Fact]
    public void Validation_ValidateIssuer_ShouldBeTrue()
    {
        Constants.GuardhouseConstants.Validation.ValidateIssuer.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateAudience_ShouldBeTrue()
    {
        Constants.GuardhouseConstants.Validation.ValidateAudience.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateLifetime_ShouldBeTrue()
    {
        Constants.GuardhouseConstants.Validation.ValidateLifetime.Should().BeTrue();
    }

    [Fact]
    public void Validation_ValidateIssuerSigningKey_ShouldBeTrue()
    {
        Constants.GuardhouseConstants.Validation.ValidateIssuerSigningKey.Should().BeTrue();
    }
}
