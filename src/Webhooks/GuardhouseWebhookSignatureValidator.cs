namespace Guardhouse.SDK.Webhooks;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Validates Guardhouse webhook signatures produced in the X-Hub-Signature header.
/// </summary>
public static class GuardhouseWebhookSignatureValidator
{
    private const string TimestampComponent = "t";
    private const string SignatureComponent = "v1";
    private const int HmacSha256ByteLength = 32;
    private const int HmacSha256HexLength = HmacSha256ByteLength * 2;
    private const byte SignedPayloadSeparator = (byte)'.';
    private const int UnixTimestampMaxByteLength = 20;
    private const int StreamBufferSize = 81920;
    private static readonly byte[] SignedPayloadSeparatorBytes = [SignedPayloadSeparator];

    /// <summary>
    /// Validates a Guardhouse webhook signature against a request body string.
    /// </summary>
    /// <remarks>
    /// Prefer the byte overload or IsValidAsync(HttpRequest, ...) so validation uses the exact raw bytes received
    /// from Guardhouse. Converting a request body to string and back to UTF-8 can change malformed or transformed
    /// payloads and cause valid signatures to fail.
    /// </remarks>
    /// <param name="rawBody">The exact request body received from Guardhouse.</param>
    /// <param name="headerValue">The value of the X-Hub-Signature header.</param>
    /// <param name="secret">The webhook secret configured for the subscription. String secrets cannot be erased from managed memory.</param>
    /// <param name="toleranceSeconds">The allowed clock skew, in seconds. Defaults to five minutes.</param>
    /// <param name="now">Optional current time override, primarily for tests.</param>
    /// <returns>True when the signature is authentic and within the timestamp tolerance; otherwise false.</returns>
    [Obsolete("Validate against raw request bytes or use IsValidAsync(HttpRequest, ...) to avoid payload encoding changes.")]
    public static bool IsValid(
        string rawBody,
        string headerValue,
        string secret,
        int toleranceSeconds = GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(rawBody))
        {
            return false;
        }

        var rawBodyBytes = Encoding.UTF8.GetBytes(rawBody);
        return IsValid(rawBodyBytes, headerValue, secret, toleranceSeconds, now);
    }

    /// <summary>
    /// Validates a Guardhouse webhook signature against the exact raw request body bytes.
    /// </summary>
    /// <param name="rawBody">The exact request body bytes received from Guardhouse.</param>
    /// <param name="headerValue">The value of the X-Hub-Signature header.</param>
    /// <param name="secret">The webhook secret configured for the subscription. String secrets cannot be erased from managed memory.</param>
    /// <param name="toleranceSeconds">The allowed clock skew, in seconds. Defaults to five minutes.</param>
    /// <param name="now">Optional current time override, primarily for tests.</param>
    /// <returns>True when the signature is authentic and within the timestamp tolerance; otherwise false.</returns>
    public static bool IsValid(
        ReadOnlySpan<byte> rawBody,
        string headerValue,
        string secret,
        int toleranceSeconds = GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            return IsValid(rawBody, headerValue, secretBytes, toleranceSeconds, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    /// <summary>
    /// Validates a Guardhouse webhook signature against the exact raw request body bytes.
    /// </summary>
    /// <param name="rawBody">The exact request body bytes received from Guardhouse.</param>
    /// <param name="headerValue">The value of the X-Hub-Signature header.</param>
    /// <param name="secret">The webhook secret bytes configured for the subscription. The caller owns and may erase this buffer.</param>
    /// <param name="toleranceSeconds">The allowed clock skew, in seconds. Defaults to five minutes.</param>
    /// <param name="now">Optional current time override, primarily for tests.</param>
    /// <returns>True when the signature is authentic and within the timestamp tolerance; otherwise false.</returns>
    public static bool IsValid(
        ReadOnlySpan<byte> rawBody,
        string headerValue,
        ReadOnlySpan<byte> secret,
        int toleranceSeconds = GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds,
        DateTimeOffset? now = null)
    {
        if (rawBody.IsEmpty || secret.IsEmpty)
        {
            return false;
        }

        if (!TryParseHeader(headerValue, out var timestamp, out var signatureCount) ||
            signatureCount == 0 ||
            !IsTimestampWithinTolerance(timestamp, toleranceSeconds, now ?? DateTimeOffset.UtcNow))
        {
            return false;
        }

        Span<byte> computedSignature = stackalloc byte[HmacSha256ByteLength];
        ComputeSignature(timestamp, rawBody, secret, computedSignature);
        try
        {
            return MatchesAnySignature(headerValue, computedSignature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computedSignature);
        }
    }

    /// <summary>
    /// Reads and validates the current ASP.NET Core request body, then rewinds the body stream.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="secret">The webhook secret configured for the subscription. String secrets cannot be erased from managed memory.</param>
    /// <param name="toleranceSeconds">The allowed clock skew, in seconds. Defaults to five minutes.</param>
    /// <param name="now">Optional current time override, primarily for tests.</param>
    /// <param name="maxBodySizeBytes">Optional maximum accepted body size. Defaults to five megabytes. Requests over this size return false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the request signature is authentic and within the timestamp tolerance; otherwise false.</returns>
    public static async Task<bool> IsValidAsync(
        HttpRequest request,
        string secret,
        int toleranceSeconds = GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds,
        DateTimeOffset? now = null,
        long? maxBodySizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            return await IsValidAsync(
                    request,
                    secretBytes,
                    toleranceSeconds,
                    now,
                    maxBodySizeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    /// <summary>
    /// Reads and validates the current ASP.NET Core request body, then rewinds the body stream.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="secret">The webhook secret bytes configured for the subscription. The caller owns and may erase this buffer.</param>
    /// <param name="toleranceSeconds">The allowed clock skew, in seconds. Defaults to five minutes.</param>
    /// <param name="now">Optional current time override, primarily for tests.</param>
    /// <param name="maxBodySizeBytes">Optional maximum accepted body size. Defaults to five megabytes. Requests over this size return false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the request signature is authentic and within the timestamp tolerance; otherwise false.</returns>
    public static async Task<bool> IsValidAsync(
        HttpRequest request,
        ReadOnlyMemory<byte> secret,
        int toleranceSeconds = GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds,
        DateTimeOffset? now = null,
        long? maxBodySizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (secret.IsEmpty)
        {
            return false;
        }

        var headerValue = request.Headers[GuardhouseConstants.Headers.WebhookSignature].ToString();
        var effectiveMaxBodySizeBytes = maxBodySizeBytes ?? GuardhouseConstants.Defaults.WebhookMaxBodySizeBytes;
        if (effectiveMaxBodySizeBytes <= 0 ||
            !TryParseHeader(headerValue, out var timestamp, out var signatureCount) ||
            signatureCount == 0 ||
            !IsTimestampWithinTolerance(timestamp, toleranceSeconds, now ?? DateTimeOffset.UtcNow))
        {
            return false;
        }

        request.EnableBuffering(bufferLimit: effectiveMaxBodySizeBytes);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        try
        {
            var computedSignature = new byte[HmacSha256ByteLength];
            var signatureComputed = await ComputeSignatureAsync(
                    timestamp,
                    request.Body,
                    secret,
                    effectiveMaxBodySizeBytes,
                    computedSignature,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!signatureComputed)
            {
                return false;
            }

            try
            {
                return MatchesAnySignature(headerValue, computedSignature);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(computedSignature);
            }
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }

    private static bool TryParseHeader(
        string headerValue,
        out long timestamp,
        out int signatureCount)
    {
        timestamp = 0;
        signatureCount = 0;

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var timestampFound = false;
        var remaining = headerValue.AsSpan();

        while (remaining.Length > 0)
        {
            var commaIndex = remaining.IndexOf(',');
            var part = commaIndex < 0 ? remaining : remaining[..commaIndex];
            remaining = commaIndex < 0 ? default : remaining[(commaIndex + 1)..];

            part = part.Trim();
            if (part.IsEmpty)
            {
                continue;
            }

            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
            {
                return false;
            }

            var key = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();
            if (value.IsEmpty)
            {
                return false;
            }

            if (key.SequenceEqual(TimestampComponent))
            {
                if (timestampFound ||
                    !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out timestamp) ||
                    timestamp <= 0)
                {
                    return false;
                }

                timestampFound = true;
            }
            else if (key.SequenceEqual(SignatureComponent))
            {
                signatureCount++;
            }
        }

        return timestampFound && signatureCount > 0;
    }

    private static bool IsTimestampWithinTolerance(long timestamp, int toleranceSeconds, DateTimeOffset now)
    {
        if (toleranceSeconds < 0)
        {
            return false;
        }

        var nowUnixSeconds = now.ToUnixTimeSeconds();
        var delta = timestamp > nowUnixSeconds
            ? timestamp - nowUnixSeconds
            : nowUnixSeconds - timestamp;

        return delta <= toleranceSeconds;
    }

    private static void ComputeSignature(
        long timestamp,
        ReadOnlySpan<byte> rawBody,
        ReadOnlySpan<byte> secret,
        Span<byte> destination)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, secret);
        AppendSignedPayloadPrefix(hmac, timestamp);
        hmac.AppendData(rawBody);

        var bytesWritten = hmac.GetHashAndReset(destination);
        if (bytesWritten != HmacSha256ByteLength)
        {
            throw new CryptographicException("Failed to compute the webhook HMAC-SHA256 signature.");
        }
    }

    private static async Task<bool> ComputeSignatureAsync(
        long timestamp,
        Stream body,
        ReadOnlyMemory<byte> secret,
        long maxBodySizeBytes,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, secret.Span);
            AppendSignedPayloadPrefix(hmac, timestamp);

            long totalBytesRead = 0;
            while (true)
            {
                var bytesRead = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (totalBytesRead == 0)
                    {
                        return false;
                    }

                    var bytesWritten = hmac.GetHashAndReset(destination.Span);
                    return bytesWritten == HmacSha256ByteLength;
                }

                totalBytesRead += bytesRead;
                if (totalBytesRead > maxBodySizeBytes)
                {
                    return false;
                }

                hmac.AppendData(buffer.AsSpan(0, bytesRead));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static void AppendSignedPayloadPrefix(IncrementalHash hmac, long timestamp)
    {
        Span<byte> timestampBytes = stackalloc byte[UnixTimestampMaxByteLength];
        if (!Utf8Formatter.TryFormat(timestamp, timestampBytes, out var timestampBytesWritten))
        {
            throw new InvalidOperationException("Failed to format the webhook timestamp.");
        }

        hmac.AppendData(timestampBytes[..timestampBytesWritten]);
        hmac.AppendData(SignedPayloadSeparatorBytes);
    }

    private static bool MatchesAnySignature(string headerValue, ReadOnlySpan<byte> computedSignature)
    {
        var remaining = headerValue.AsSpan();
        Span<byte> signatureBytes = stackalloc byte[HmacSha256ByteLength];

        while (remaining.Length > 0)
        {
            var commaIndex = remaining.IndexOf(',');
            var part = commaIndex < 0 ? remaining : remaining[..commaIndex];
            remaining = commaIndex < 0 ? default : remaining[(commaIndex + 1)..];

            part = part.Trim();
            if (part.IsEmpty)
            {
                continue;
            }

            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == part.Length - 1)
            {
                return false;
            }

            var key = part[..separatorIndex].Trim();
            if (!key.SequenceEqual(SignatureComponent))
            {
                continue;
            }

            signatureBytes.Clear();
            if (TryDecodeHexSignature(part[(separatorIndex + 1)..].Trim(), signatureBytes) &&
                CryptographicOperations.FixedTimeEquals(computedSignature, signatureBytes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryDecodeHexSignature(ReadOnlySpan<char> signature, Span<byte> destination)
    {
        if (signature.Length != HmacSha256HexLength)
        {
            return false;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            var high = GetHexValue(signature[i * 2]);
            var low = GetHexValue(signature[(i * 2) + 1]);
            if (high < 0 || low < 0)
            {
                destination.Clear();
                return false;
            }

            destination[i] = (byte)((high << 4) | low);
        }

        return true;
    }

    private static int GetHexValue(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return value - '0';
        }

        if (value is >= 'a' and <= 'f')
        {
            return value - 'a' + 10;
        }

        if (value is >= 'A' and <= 'F')
        {
            return value - 'A' + 10;
        }

        return -1;
    }
}
