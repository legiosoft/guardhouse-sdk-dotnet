namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Extensions;
using Microsoft.Extensions.Logging;
using Polly;

internal class GuardhouseJwksHttpHandler(
    HttpMessageHandler innerHandler,
    ILogger<GuardhouseJwksHttpHandler> logger,
    int maxRetryAttempts,
    IReadOnlyCollection<string> allowedHosts,
    bool requireHttps) : DelegatingHandler(innerHandler)
{
    private const int MaxKidLogCount = 10;

    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy = BuildRetryPolicy(logger, maxRetryAttempts);
    private readonly HashSet<string> _allowedHosts = new(allowedHosts, StringComparer.OrdinalIgnoreCase);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri ?? throw new InvalidOperationException("Backchannel request URI is missing.");
        var requestUrl = requestUri.ToString();
        var isWellKnownRequest = IsWellKnownRequest(requestUri);

        if (requireHttps && !string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for metadata and JWKS endpoints.");
        }

        if (_allowedHosts.Count > 0 && !_allowedHosts.Contains(requestUri.Host))
        {
            throw new InvalidOperationException($"Backchannel host is not allowed: {requestUri.Host}");
        }

        logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "Backchannel request: {Method} {Url}", request.Method, requestUrl);

        try
        {
            var response = isWellKnownRequest
                ? await _retryPolicy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken)
                : await base.SendAsync(request, cancellationToken);

            logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "Backchannel response: {StatusCode} from {Url}", (int)response.StatusCode, requestUrl);

            if (isWellKnownRequest && response.IsSuccessStatusCode && logger.IsEnabled(LogLevel.Debug))
            {
                await LogWellKnownResponseAsync(response, cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            if (isWellKnownRequest)
            {
                logger.LogError(ex, "JWKS request failed: {Url}", requestUrl);
            }

            throw;
        }
    }

    private static bool IsWellKnownRequest(Uri? requestUri)
    {
        if (requestUri == null)
        {
            return false;
        }

        var path = requestUri.AbsolutePath;
        return path.Contains(GuardhouseConstants.Endpoints.WellKnownOpenIdConfiguration, StringComparison.OrdinalIgnoreCase) ||
               path.Contains(GuardhouseConstants.Endpoints.WellKnownJwks, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogWellKnownResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        logger.LogDebug("Well-known response length: {Length} bytes", contentBytes.Length);

        try
        {
            using var document = JsonDocument.Parse(contentBytes);
            var root = document.RootElement;

            var jwksUri = TryExtractJwksUri(root);
            if (!string.IsNullOrEmpty(jwksUri))
            {
                logger.LogDebug("OpenID config points to JWKS URI: {JwksUri}", jwksUri);
                return;
            }

            var kids = new List<string>(MaxKidLogCount);
            var kidCount = ExtractKids(root, kids);
            if (kidCount == 0)
            {
                logger.LogWarning("JWKS response contains no kids");
                return;
            }

            logger.LogDebug("JWKS contains {KidCount} keys", kidCount);
            if (kids.Count > 0)
            {
                logger.LogDebug("JWKS kids (first {Count}): {Kids}", kids.Count, string.Join(", ", kids));
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Well-known response is not valid JSON.");
        }
    }

    private static string? TryExtractJwksUri(JsonElement root)
    {
        if (TryGetProperty(root, "jwks_uri", out var jwksUriElement) &&
            jwksUriElement.ValueKind == JsonValueKind.String)
        {
            return jwksUriElement.GetString();
        }

        return null;
    }

    private static int ExtractKids(JsonElement root, List<string> kids)
    {
        if (!TryGetProperty(root, "keys", out var keysElement) ||
            keysElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var kidCount = 0;
        var distinctKids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keysElement.EnumerateArray())
        {
            if (key.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryGetProperty(key, "kid", out var kidElement) &&
                kidElement.ValueKind == JsonValueKind.String)
            {
                var kid = kidElement.GetString();
                if (!string.IsNullOrWhiteSpace(kid))
                {
                    kidCount++;
                    if (distinctKids.Add(kid) && kids.Count < MaxKidLogCount)
                    {
                        kids.Add(kid);
                    }
                }
            }
        }

        return kidCount;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(
        ILogger<GuardhouseJwksHttpHandler> logger,
        int maxRetryAttempts)
    {
        if (maxRetryAttempts <= 0)
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }

        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && ((int)r.StatusCode >= 500 || (int)r.StatusCode == 429))
            .WaitAndRetryAsync(
                maxRetryAttempts,
                (int retryAttempt, DelegateResult<HttpResponseMessage> result, Context _) =>
                    GetRetryDelay(result, retryAttempt),
                (DelegateResult<HttpResponseMessage> result, TimeSpan timeSpan, int retryCount, Context _) =>
                {
                    logger.LogWarning(
                        "JWKS request failed, retrying in {Delay}s. Attempt {Attempt}/{MaxAttempts}. Status: {StatusCode}",
                        timeSpan.TotalSeconds, retryCount, maxRetryAttempts, result.Result?.StatusCode);
                    return Task.CompletedTask;
                });
    }

    private static TimeSpan GetRetryDelay(DelegateResult<HttpResponseMessage> result, int retryAttempt)
    {
        if (result.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var retryAfter = TryGetRetryAfterDelay(result.Result);
            if (retryAfter.HasValue)
            {
                return retryAfter.Value;
            }
        }

        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
    }

    private static TimeSpan? TryGetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return null;
        }

        if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
        {
            return retryAfter.Delta.Value;
        }

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return null;
    }
}
