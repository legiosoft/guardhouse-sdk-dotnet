using System.Text.Json;
using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Guardhouse.SDK.Models;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class IntrospectionResponseTests
{
    private readonly FakeClock _clock = new FakeClock(SystemClock.Instance.GetCurrentInstant());

    [Fact]
    public void ExpiresAt_WhenExpIsNull_ShouldReturnNull()
    {
        var response = new IntrospectionResponse
        {
            Active = true,
            Exp = null
        };

        response.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ExpiresAt_WhenExpHasValue_ShouldCalculateCorrectInstant()
    {
        var expTimestamp = 1735689600L; // 2025-01-01 00:00:00 UTC
        var expectedInstant = Instant.FromUnixTimeSeconds(expTimestamp);

        var response = new IntrospectionResponse
        {
            Active = true,
            Exp = expTimestamp
        };

        response.ExpiresAt.Should().Be(expectedInstant);
    }

    [Fact]
    public void IssuedAt_WhenIatIsNull_ShouldReturnNull()
    {
        var response = new IntrospectionResponse
        {
            Active = true,
            Iat = null
        };

        response.IssuedAt.Should().BeNull();
    }

    [Fact]
    public void IssuedAt_WhenIatHasValue_ShouldCalculateCorrectInstant()
    {
        var iatTimestamp = 1735603200L; // 2024-12-31 00:00:00 UTC
        var expectedInstant = Instant.FromUnixTimeSeconds(iatTimestamp);

        var response = new IntrospectionResponse
        {
            Active = true,
            Iat = iatTimestamp
        };

        response.IssuedAt.Should().Be(expectedInstant);
    }

    [Fact]
    public void NotBefore_WhenNbfIsNull_ShouldReturnNull()
    {
        var response = new IntrospectionResponse
        {
            Active = true,
            Nbf = null
        };

        response.NotBefore.Should().BeNull();
    }

    [Fact]
    public void NotBefore_WhenNbfHasValue_ShouldCalculateCorrectInstant()
    {
        var nbfTimestamp = 1735603200L; // 2024-12-31 00:00:00 UTC
        var expectedInstant = Instant.FromUnixTimeSeconds(nbfTimestamp);

        var response = new IntrospectionResponse
        {
            Active = true,
            Nbf = nbfTimestamp
        };

        response.NotBefore.Should().Be(expectedInstant);
    }

    [Fact]
    public void AllTimestampFields_ShouldCalculateCorrectly_WhenAllHaveValues()
    {
        var expTimestamp = 1735689600L;
        var iatTimestamp = 1735603200L;
        var nbfTimestamp = 1735603200L;

        var response = new IntrospectionResponse
        {
            Active = true,
            Exp = expTimestamp,
            Iat = iatTimestamp,
            Nbf = nbfTimestamp
        };

        response.ExpiresAt.Should().Be(Instant.FromUnixTimeSeconds(expTimestamp));
        response.IssuedAt.Should().Be(Instant.FromUnixTimeSeconds(iatTimestamp));
        response.NotBefore.Should().Be(Instant.FromUnixTimeSeconds(nbfTimestamp));
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var response = new IntrospectionResponse();

        response.Active.Should().BeFalse();
        response.Scope.Should().BeNull();
        response.ClientId.Should().BeNull();
        response.Username.Should().BeNull();
        response.TokenType.Should().BeNull();
        response.Algorithm.Should().BeNull();
        response.Signature.Should().BeNull();
        response.Exp.Should().BeNull();
        response.Iat.Should().BeNull();
        response.Nbf.Should().BeNull();
        response.Sub.Should().BeNull();
        response.Aud.Should().BeNull();
        response.Iss.Should().BeNull();
        response.Jti.Should().BeNull();
        response.Role.Should().BeNull();
        response.Roles.Should().BeNull();
        response.AdditionalClaims.Should().BeNull();
        response.ExpiresAt.Should().BeNull();
        response.IssuedAt.Should().BeNull();
        response.NotBefore.Should().BeNull();
    }

    [Fact]
    public void AdditionalClaims_ShouldCaptureUnknownIntrospectionFields()
    {
        const string json = """
                            {
                              "active": true,
                              "scope": "system_api",
                              "system": "system_administrator",
                              "business": ["retail", "wholesale"]
                            }
                            """;

        var response = JsonSerializer.Deserialize<IntrospectionResponse>(json);

        response.Should().NotBeNull();
        var additionalClaims = response!.AdditionalClaims;
        additionalClaims.Should().NotBeNull();

        var claims = additionalClaims!;
        claims.Should().ContainKey("system");
        claims.Should().ContainKey("business");
        claims["system"].GetString().Should().Be("system_administrator");
        claims["business"].ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void Aud_ShouldDeserializeFromString()
    {
        const string json = """
                            {
                              "active": true,
                              "aud": "resource-api"
                            }
                            """;

        var response = JsonSerializer.Deserialize<IntrospectionResponse>(json);

        response.Should().NotBeNull();
        response!.Aud.Should().Equal("resource-api");
    }

    [Fact]
    public void Aud_ShouldDeserializeFromArray()
    {
        const string json = """
                            {
                              "active": true,
                              "aud": [
                                "resource-api",
                                "guardhouse-api"
                              ]
                            }
                            """;

        var response = JsonSerializer.Deserialize<IntrospectionResponse>(json);

        response.Should().NotBeNull();
        response!.Aud.Should().Equal("resource-api", "guardhouse-api");
    }
}
