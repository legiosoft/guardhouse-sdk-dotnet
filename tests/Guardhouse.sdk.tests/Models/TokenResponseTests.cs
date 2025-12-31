using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

/// <summary>
/// Unit tests for SDK models
/// </summary>
public class TokenResponseTests
{
    [Fact]
    public void ExpiresAt_ShouldReturnCorrectInstant()
    {
        var token = new TokenResponse
        {
            AccessToken = "test_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        var now = SystemClock.Instance.GetCurrentInstant();
        var expected = now.Plus(Duration.FromSeconds(3600));

        var difference = (token.ExpiresAt - expected);
        if (difference < Duration.Zero)
        {
            difference = -difference;
        }
        difference.Should().BeLessThan(Duration.FromSeconds(1));
    }

    [Fact]
    public void ExpiresAtDateTime_ShouldReturnCorrectDateTime()
    {
        var token = new TokenResponse
        {
            AccessToken = "test_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };

        var expiresAt = token.ExpiresAtDateTime;

        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsExpired_WithNonExpiredToken_ShouldReturnFalse()
    {
        var token = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = 3600
        };

        token.IsExpired().Should().BeFalse();
        token.IsExpired(0).Should().BeFalse();
        token.IsExpired(60).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithExpiredToken_ShouldReturnTrue()
    {
        var token = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = -1
        };

        token.IsExpired().Should().BeTrue();
        token.IsExpired(0).Should().BeTrue();
        token.IsExpired(120).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WithBuffer_ShouldReturnTrue()
    {
        var token = new TokenResponse
        {
            AccessToken = "test_token",
            ExpiresIn = 60
        };

        token.IsExpired().Should().BeTrue();
        token.IsExpired(0).Should().BeFalse();
        token.IsExpired(30).Should().BeFalse();
        token.IsExpired(61).Should().BeTrue();
    }
}