namespace Guardhouse.SDK.Services;

using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static class GuardhouseErrorResponseSanitizer
{
    private const string ErrorPrefix = "Error: ";
    private const string ErrorPropertyName = "error";
    private const string EmptyResponseBodyMessage = "Response body is empty.";
    private const string OmittedResponseBodyMessage = "Response body omitted for security.";
    private const int MaxSafeErrorCodeLength = 64;
    private const int MaxResponseContentLength = 8 * 1024;
    private const int MaxJsonDepth = 8;
    private const int ReadBufferSize = 1024;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaxJsonDepth
    };

    public static string Summarize(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return EmptyResponseBodyMessage;
        }

        if (responseContent.Length > MaxResponseContentLength)
        {
            return OmittedResponseBodyMessage;
        }

        return TryReadSafeErrorCode(responseContent, out var errorCode)
            ? ErrorPrefix + errorCode
            : OmittedResponseBodyMessage;
    }

    public static async Task<string> SummarizeAsync(HttpContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Headers.ContentLength == 0)
        {
            return EmptyResponseBodyMessage;
        }

        if (content.Headers.ContentLength > MaxResponseContentLength)
        {
            return OmittedResponseBodyMessage;
        }

        var readResult = await ReadLimitedContentAsync(content, cancellationToken);
        if (readResult.IsTruncated)
        {
            return OmittedResponseBodyMessage;
        }

        return Summarize(readResult.Content);
    }

    private static bool TryReadSafeErrorCode(string responseContent, out string? errorCode)
    {
        errorCode = null;

        try
        {
            using var errorDocument = JsonDocument.Parse(responseContent, JsonOptions);
            var rootElement = errorDocument.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object ||
                !rootElement.TryGetProperty(ErrorPropertyName, out var errorProperty) ||
                errorProperty.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var candidate = errorProperty.GetString();
            if (!IsSafeErrorCode(candidate))
            {
                return false;
            }

            errorCode = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsSafeErrorCode(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxSafeErrorCodeLength)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            if (!IsAllowedErrorCodeCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<LimitedContentReadResult> ReadLimitedContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(
            stream,
            GetEncoding(content),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: ReadBufferSize,
            leaveOpen: true);

        var buffer = ArrayPool<char>.Shared.Rent(ReadBufferSize);
        try
        {
            var builder = new StringBuilder(GetInitialBuilderCapacity(content));
            var remaining = MaxResponseContentLength;

            while (remaining > 0)
            {
                var count = Math.Min(ReadBufferSize, remaining);
                var charsRead = await reader.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
                if (charsRead == 0)
                {
                    return new LimitedContentReadResult(builder.ToString(), false);
                }

                builder.Append(buffer, 0, charsRead);
                remaining -= charsRead;
            }

            var overflowCharsRead = await reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
            return new LimitedContentReadResult(builder.ToString(), overflowCharsRead != 0);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static int GetInitialBuilderCapacity(HttpContent content)
    {
        var contentLength = content.Headers.ContentLength;
        if (contentLength is > 0 and <= MaxResponseContentLength)
        {
            return (int)contentLength.Value;
        }

        return ReadBufferSize;
    }

    private static Encoding GetEncoding(HttpContent content)
    {
        var charset = content.Headers.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
        catch (NotSupportedException)
        {
            return Encoding.UTF8;
        }
    }

    private static bool IsAllowedErrorCodeCharacter(char character)
    {
        return character switch
        {
            >= 'A' and <= 'Z' => true,
            >= 'a' and <= 'z' => true,
            >= '0' and <= '9' => true,
            '_' or '-' or '.' => true,
            _ => false
        };
    }

    private readonly record struct LimitedContentReadResult(string Content, bool IsTruncated);
}
