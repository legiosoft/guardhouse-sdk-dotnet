using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Models.Users;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class UserStatusJsonConverterTests
{
    [Fact]
    public void Deserialize_WithStringValue_ShouldParseKnownStatus()
    {
        const string json = """
                            {
                              "status": "active"
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Deserialize_WithNumericValue_ShouldParseKnownStatus()
    {
        const string json = """
                            {
                              "status": 2
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public void Deserialize_WithUnknownStringStatus_ShouldFallbackToUnknown()
    {
        const string json = """
                            {
                              "status": "retired"
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Unknown);
    }

    [Fact]
    public void Deserialize_WithUnknownNumericStatus_ShouldFallbackToUnknown()
    {
        const string json = """
                            {
                              "status": 999
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Unknown);
    }

    [Fact]
    public void Deserialize_WithKnownAlias_ShouldMapToExpectedStatus()
    {
        const string json = """
                            {
                              "status": "locked_out"
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Locked);
    }
}
