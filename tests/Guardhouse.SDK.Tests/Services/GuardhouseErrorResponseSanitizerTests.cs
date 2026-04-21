using FluentAssertions;
using Guardhouse.SDK.Services;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseErrorResponseSanitizerTests
{
    [Fact]
    public void Summarize_WhenResponseBodyIsEmpty_ShouldReturnEmptyMessage()
    {
        var summary = GuardhouseErrorResponseSanitizer.Summarize(null);

        summary.Should().Be("Response body is empty.");
    }

    [Fact]
    public void Summarize_WhenResponseContainsSafeErrorCode_ShouldReturnErrorCode()
    {
        var summary = GuardhouseErrorResponseSanitizer.Summarize("""
            {"error":"invalid_client","error_description":"client_secret=super-secret"}
            """);

        summary.Should().Be("Error: invalid_client");
    }

    [Fact]
    public void Summarize_WhenResponseIsPlainText_ShouldOmitResponseBody()
    {
        var summary = GuardhouseErrorResponseSanitizer.Summarize("password=SuperSecret123!");

        summary.Should().Be("Response body omitted for security.");
    }

    [Fact]
    public void Summarize_WhenErrorCodeContainsUnsafeCharacters_ShouldOmitResponseBody()
    {
        var summary = GuardhouseErrorResponseSanitizer.Summarize("""
            {"error":"client_secret=super-secret"}
            """);

        summary.Should().Be("Response body omitted for security.");
    }
}
