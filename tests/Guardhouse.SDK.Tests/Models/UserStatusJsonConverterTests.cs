using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Models.Users;
using Xunit;

namespace Guardhouse.SDK.Tests.Models;

public class UserStatusJsonConverterTests
{
    [Theory]
    [InlineData("staged", UserStatus.Staged)]
    [InlineData("invited", UserStatus.Invited)]
    [InlineData("active", UserStatus.Active)]
    [InlineData("archived", UserStatus.Archived)]
    [InlineData("In Progress", UserStatus.Unknown)]
    public void Deserialize_WithStringValue_ShouldParseLifecycleStatus(string status, UserStatus expected)
    {
        var json = $$"""
                     {
                       "status": "{{status}}"
                     }
                     """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, UserStatus.Staged)]
    [InlineData(2, UserStatus.Invited)]
    [InlineData(3, UserStatus.Active)]
    [InlineData(4, UserStatus.Archived)]
    public void Deserialize_WithNumericValue_ShouldParseGuardhouseServerStatus(int status, UserStatus expected)
    {
        var json = $$"""
                     {
                       "status": {{status}}
                     }
                     """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", UserStatus.Staged)]
    [InlineData("2", UserStatus.Invited)]
    [InlineData("3", UserStatus.Active)]
    [InlineData("4", UserStatus.Archived)]
    public void Deserialize_WithNumericStringValue_ShouldParseGuardhouseServerStatus(
        string status,
        UserStatus expected)
    {
        var json = $$"""
                     {
                       "status": "{{status}}"
                     }
                     """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData("retired")]
    [InlineData("inactive")]
    [InlineData("disabled")]
    [InlineData("locked")]
    [InlineData("locked_out")]
    [InlineData("suspended")]
    public void Deserialize_WithNonLifecycleStringStatus_ShouldFallbackToUnknown(string status)
    {
        var json = $$"""
                     {
                       "status": "{{status}}"
                     }
                     """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Unknown);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(-1)]
    public void Deserialize_WithUnknownNumericStatus_ShouldFallbackToUnknown(int status)
    {
        var json = $$"""
                     {
                       "status": {{status}}
                     }
                     """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Unknown);
    }

    [Theory]
    [InlineData(UserStatus.Staged, "\"staged\"")]
    [InlineData(UserStatus.Invited, "\"invited\"")]
    [InlineData(UserStatus.Active, "\"active\"")]
    [InlineData(UserStatus.Archived, "\"archived\"")]
    [InlineData(UserStatus.Unknown, "\"unknown\"")]
    public void Serialize_ShouldWriteLifecycleStatusString(UserStatus status, string expectedJson)
    {
        var json = JsonSerializer.Serialize(status);

        json.Should().Be(expectedJson);
    }

    [Fact]
    public void Deserialize_GetUserByIdResponse_ShouldReadStatusAndAccessFlagsSeparately()
    {
        const string json = """
                            {
                              "status": 2,
                              "isSuspended": true,
                              "isLocked": false
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetUserByIdResponse>(json);

        response.Should().NotBeNull();
        response!.Status.Should().Be(UserStatus.Invited);
        response.IsSuspended.Should().BeTrue();
        response.IsLocked.Should().BeFalse();
    }
}
