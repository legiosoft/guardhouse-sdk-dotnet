using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Guardhouse.SDK.Models;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class TokenResponseTests
{
    private readonly FakeClock _clock = new FakeClock(SystemClock.Instance.GetCurrentInstant());

    [Fact]
    public void TokenResponse_DefaultValues_ShouldBeCorrect()
    {
        var response = new TokenResponse
        {
            AccessToken = "test_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        response.AccessToken.Should().Be("test_token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public void TokenResponse_ExpiresAt_ShouldCalculateCorrectly()
    {
        var response = new TokenResponse
        {
            ExpiresIn = 3600
        }.WithClock(_clock);

        var expectedExpiresIn = Duration.FromSeconds(3600);

        var expiresAt = response.ExpiresAt;
        var now = _clock.GetCurrentInstant();
        var expectedExpiresAt = now.Plus(expectedExpiresIn);

        var difference = Duration.FromSeconds(Math.Abs((expiresAt - expectedExpiresAt).TotalSeconds));
        difference.Should().BeLessThan(Duration.FromSeconds(1));
    }

    [Fact]
    public void TokenResponse_IsExpired_WithValidToken_ShouldReturnFalse()
    {
        var response = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = 3600
        }.WithClock(_clock);

        var isExpired = response.IsExpired(bufferSeconds: 300);

        isExpired.Should().BeFalse();
    }

    [Fact]
    public void TokenResponse_IsExpired_WithExpiredToken_ShouldReturnTrue()
    {
        var response = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = -600
        }.WithClock(_clock);

        var isExpired = response.IsExpired(bufferSeconds: 0);

        isExpired.Should().BeTrue();
    }

    [Fact]
    public void TokenResponse_IsExpired_WithBuffer_ShouldConsiderBuffer()
    {
        var response = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = 100
        }.WithClock(_clock);

        var isExpiredWithoutBuffer = response.IsExpired(bufferSeconds: 0);
        var isExpiredWithBuffer = response.IsExpired(bufferSeconds: 120);

        isExpiredWithoutBuffer.Should().BeFalse();
        isExpiredWithBuffer.Should().BeTrue();
    }
}
