using FluentAssertions;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Webhooks;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Guardhouse.SDK.Tests.Webhooks;

public class GuardhouseWebhookSignatureValidatorTests
{
    private const string Secret = "whsec_test_secret_that_is_long_enough_for_hmac";
    private const string RawBody = "{\"eventType\":0,\"data\":{\"userId\":123,\"email\":\"ada@example.com\"},\"timestamp\":1710000000}";

    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_710_000_000);
    private static readonly byte[] SecretBytes = Encoding.UTF8.GetBytes(Secret);
    private static readonly byte[] RawBodyBytes = Encoding.UTF8.GetBytes(RawBody);

    [Fact]
    public void IsValid_WhenSignatureMatches_ShouldReturnTrue()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenSignatureIsUppercase_ShouldReturnTrue()
    {
        var timestamp = Now.ToUnixTimeSeconds();
        var header = $"t={timestamp},v1={ComputeSignature(timestamp, RawBody, Secret).ToUpperInvariant()}";

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenOneOfMultipleV1SignaturesMatches_ShouldReturnTrue()
    {
        var timestamp = Now.ToUnixTimeSeconds();
        var validSignature = ComputeSignature(timestamp, RawBody, Secret);
        var header = $"t={timestamp},v1={new string('0', 64)},v1={validSignature}";

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithStringSecret_WhenSignatureMatches_ShouldReturnTrue()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, Secret, now: Now);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenBodyIsChanged_ShouldReturnFalse()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);
        var changedBody = Encoding.UTF8.GetBytes(
            RawBody.Replace("ada@example.com", "mallory@example.com", StringComparison.Ordinal));

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(
            changedBody,
            header,
            SecretBytes,
            now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSecretDoesNotMatch_ShouldReturnFalse()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, "wrong-secret", now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenTimestampIsOlderThanTolerance_ShouldReturnFalse()
    {
        var timestamp = Now.AddSeconds(-(GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds + 1))
            .ToUnixTimeSeconds();
        var header = CreateHeader(timestamp, RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenTimestampIsNewerThanTolerance_ShouldReturnFalse()
    {
        var timestamp = Now.AddSeconds(GuardhouseConstants.Defaults.WebhookSignatureToleranceSeconds + 1)
            .ToUnixTimeSeconds();
        var header = CreateHeader(timestamp, RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("t=1710000000")]
    [InlineData("v1=4dff")]
    [InlineData("t=abc,v1=4dff")]
    [InlineData("t=1710000000,v1=4dff")]
    [InlineData("t=1710000000,broken")]
    [InlineData("t=1710000000,t=1710000001,v1=4dff")]
    public void IsValid_WhenHeaderIsMalformed_ShouldReturnFalse(string header)
    {
        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSignatureIsNotHex_ShouldReturnFalse()
    {
        var header = $"t={Now.ToUnixTimeSeconds()},v1={new string('z', 64)}";

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(RawBodyBytes, header, SecretBytes, now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenBodyIsEmpty_ShouldReturnFalse()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), string.Empty, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(Array.Empty<byte>(), header, SecretBytes, now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNegativeTolerance_ShouldReturnFalse()
    {
        var header = CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = GuardhouseWebhookSignatureValidator.IsValid(
            RawBodyBytes,
            header,
            SecretBytes,
            toleranceSeconds: -1,
            now: Now);

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenRequestSignatureMatches_ShouldReturnTrueAndRewindBody()
    {
        var context = CreateHttpContext(RawBody);
        context.Request.Headers[GuardhouseConstants.Headers.WebhookSignature] =
            CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = await GuardhouseWebhookSignatureValidator.IsValidAsync(context.Request, SecretBytes, now: Now);

        isValid.Should().BeTrue();
        context.Request.Body.Position.Should().Be(0);

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
        var bodyAfterValidation = await reader.ReadToEndAsync();
        bodyAfterValidation.Should().Be(RawBody);
    }

    [Fact]
    public async Task IsValidAsync_WhenBodyExceedsLimit_ShouldReturnFalseAndRewindBody()
    {
        var context = CreateHttpContext(RawBody);
        context.Request.Headers[GuardhouseConstants.Headers.WebhookSignature] =
            CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = await GuardhouseWebhookSignatureValidator.IsValidAsync(
            context.Request,
            Secret,
            now: Now,
            maxBodySizeBytes: Encoding.UTF8.GetByteCount(RawBody) - 1);

        isValid.Should().BeFalse();
        context.Request.Body.Position.Should().Be(0);
    }

    [Fact]
    public async Task IsValidAsync_WhenUsingStringSecret_ShouldReturnTrue()
    {
        var context = CreateHttpContext(RawBody);
        context.Request.Headers[GuardhouseConstants.Headers.WebhookSignature] =
            CreateHeader(Now.ToUnixTimeSeconds(), RawBody, Secret);

        var isValid = await GuardhouseWebhookSignatureValidator.IsValidAsync(context.Request, Secret, now: Now);

        isValid.Should().BeTrue();
    }

    private static DefaultHttpContext CreateHttpContext(string body)
    {
        var context = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.ContentType = "application/json";
        return context;
    }

    private static string CreateHeader(long timestamp, string body, string secret)
    {
        return $"t={timestamp},v1={ComputeSignature(timestamp, body, secret)}";
    }

    private static string ComputeSignature(long timestamp, string body, string secret)
    {
        var signedPayload = $"{timestamp}.{body}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }
}
