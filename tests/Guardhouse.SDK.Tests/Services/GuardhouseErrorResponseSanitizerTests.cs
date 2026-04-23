using FluentAssertions;
using Guardhouse.SDK.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    [Theory]
    [InlineData("""{"error":500}""")]
    [InlineData("""{"error":{"code":"AUTH_FAIL"}}""")]
    [InlineData("""{"error":["AUTH_FAIL"]}""")]
    public void Summarize_WhenErrorPropertyIsNotString_ShouldNotThrow(string payload)
    {
        var action = () => GuardhouseErrorResponseSanitizer.Summarize(payload);

        action.Should().NotThrow();
        action().Should().Be("Response body omitted for security.");
    }

    [Fact]
    public void Summarize_WhenResponseBodyExceedsLimit_ShouldOmitResponseBody()
    {
        var oversizedPayload = new string('a', 8193);

        var summary = GuardhouseErrorResponseSanitizer.Summarize(oversizedPayload);

        summary.Should().Be("Response body omitted for security.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenContentLengthExceedsLimit_ShouldNotReadBody()
    {
        using var content = new ThrowIfReadHttpContent(8193);

        var summary = await GuardhouseErrorResponseSanitizer.SummarizeAsync(content, CancellationToken.None);

        summary.Should().Be("Response body omitted for security.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenContentLengthIsUnknown_ShouldNotReadFullBody()
    {
        var payload = new string('a', 20000);
        using var content = new CountingHttpContent(payload);

        var summary = await GuardhouseErrorResponseSanitizer.SummarizeAsync(content, CancellationToken.None);

        summary.Should().Be("Response body omitted for security.");
        content.TotalBytesRead.Should().BeLessThan(payload.Length);
    }

    private sealed class ThrowIfReadHttpContent(long contentLength) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = contentLength;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new InvalidOperationException("Content should not be read when Content-Length exceeds the configured limit.");
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            throw new InvalidOperationException("Content stream should not be requested when Content-Length exceeds the configured limit.");
        }
    }

    private sealed class CountingHttpContent : HttpContent
    {
        private readonly byte[] _payloadBytes;

        public CountingHttpContent(string payload)
        {
            _payloadBytes = Encoding.UTF8.GetBytes(payload);
            Headers.ContentType = new MediaTypeHeaderValue("text/plain")
            {
                CharSet = Encoding.UTF8.WebName
            };
        }

        public int TotalBytesRead { get; private set; }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new InvalidOperationException("The sanitizer should read from the content stream directly.");
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(new CountingReadStream(_payloadBytes, this));
        }

        private sealed class CountingReadStream(byte[] buffer, CountingHttpContent owner) : MemoryStream(buffer, writable: false)
        {
            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = base.Read(buffer, offset, count);
                owner.TotalBytesRead += bytesRead;
                return bytesRead;
            }

            public override int Read(Span<byte> buffer)
            {
                var bytesRead = base.Read(buffer);
                owner.TotalBytesRead += bytesRead;
                return bytesRead;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var bytesRead = base.Read(buffer.Span);
                owner.TotalBytesRead += bytesRead;
                return ValueTask.FromResult(bytesRead);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = base.Read(buffer, offset, count);
                owner.TotalBytesRead += bytesRead;
                return Task.FromResult(bytesRead);
            }
        }
    }
}
