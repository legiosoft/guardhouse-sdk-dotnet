namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Extensions;
using Microsoft.Extensions.Logging;
using Polly;

internal class GuardhouseJwksHttpHandler : DelegatingHandler
{
    private const int MaxKidLogCount = 10;
    private const int MaxRedirectCount = 5;
    private const int MaxInspectableWellKnownResponseBytes = 1024 * 1024;
    private static readonly TimeSpan MaxRetryAfterDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<GuardhouseJwksHttpHandler> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly HashSet<string> _allowedHosts;
    private readonly bool _requireHttps;

    public GuardhouseJwksHttpHandler(
        HttpMessageHandler innerHandler,
        ILogger<GuardhouseJwksHttpHandler> logger,
        int maxRetryAttempts,
        IReadOnlyCollection<string> allowedHosts,
        bool requireHttps) : base(innerHandler)
    {
        _logger = logger;
        _retryPolicy = BuildRetryPolicy(logger, maxRetryAttempts);
        _allowedHosts = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);
        _requireHttps = requireHttps;

        if (innerHandler is HttpClientHandler httpClientHandler)
        {
            // Redirects must be evaluated by this handler so every hop is re-validated.
            httpClientHandler.AllowAutoRedirect = false;
        }
        else if (innerHandler is SocketsHttpHandler socketsHttpHandler)
        {
            // Guard against callers that pass the transport handler directly.
            socketsHttpHandler.AllowAutoRedirect = false;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentRequest = request;
        var redirectCount = 0;
        var isWellKnownRequest = IsWellKnownRequest(request.RequestUri);

        while (true)
        {
            var requestUri = currentRequest.RequestUri ?? throw new InvalidOperationException("Backchannel request URI is missing.");
            ValidateRequestUri(requestUri);

            var requestUrl = requestUri.ToString();
            _logger.LogDebugIf(_logger.IsEnabled(LogLevel.Debug), "Backchannel request: {Method} {Url}", currentRequest.Method, requestUrl);

            HttpResponseMessage response;
            try
            {
                response = isWellKnownRequest
                    ? await _retryPolicy.ExecuteAsync(ct => base.SendAsync(currentRequest, ct), cancellationToken)
                    : await base.SendAsync(currentRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                if (isWellKnownRequest)
                {
                    _logger.LogError(ex, "JWKS request failed: {Url}", requestUrl);
                }

                throw;
            }

            _logger.LogDebugIf(_logger.IsEnabled(LogLevel.Debug), "Backchannel response: {StatusCode} from {Url}", (int)response.StatusCode, requestUrl);

            if (!IsRedirectStatusCode(response.StatusCode))
            {
                if (isWellKnownRequest && response.IsSuccessStatusCode && _logger.IsEnabled(LogLevel.Debug))
                {
                    await LogWellKnownResponseAsync(response, cancellationToken);
                }

                return response;
            }

            if (redirectCount >= MaxRedirectCount)
            {
                response.Dispose();
                throw new InvalidOperationException($"Backchannel request exceeded maximum redirect count of {MaxRedirectCount}.");
            }

            if (!CanFollowRedirect(currentRequest.Method))
            {
                response.Dispose();
                throw new InvalidOperationException(
                    $"Backchannel redirects are only supported for GET and HEAD requests. Current method: {currentRequest.Method}.");
            }

            var redirectUri = ResolveRedirectUri(response, requestUri);
            _logger.LogDebugIf(
                _logger.IsEnabled(LogLevel.Debug),
                "Backchannel redirect: {StatusCode} from {From} to {To}",
                (int)response.StatusCode,
                requestUrl,
                redirectUri);

            response.Dispose();
            currentRequest = CreateRedirectRequest(currentRequest, redirectUri);
            redirectCount++;
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

    private void ValidateRequestUri(Uri requestUri)
    {
        if (_requireHttps && !string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for metadata and JWKS endpoints.");
        }

        if (_allowedHosts.Count > 0 && !_allowedHosts.Contains(requestUri.Host))
        {
            throw new InvalidOperationException($"Backchannel host is not allowed: {requestUri.Host}");
        }
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            301 => true,
            302 => true,
            303 => true,
            307 => true,
            308 => true,
            _ => false
        };
    }

    private static bool CanFollowRedirect(HttpMethod method)
    {
        return method == HttpMethod.Get || method == HttpMethod.Head;
    }

    private static Uri ResolveRedirectUri(HttpResponseMessage response, Uri requestUri)
    {
        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Backchannel redirect response did not include a Location header.");
        return location.IsAbsoluteUri ? location : new Uri(requestUri, location);
    }

    private static HttpRequestMessage CreateRedirectRequest(HttpRequestMessage request, Uri redirectUri)
    {
        var redirectedRequest = new HttpRequestMessage(request.Method, redirectUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        var isCrossAuthorityRedirect = request.RequestUri == null || !HasSameAuthority(request.RequestUri, redirectUri);

        foreach (var header in request.Headers)
        {
            if (!ShouldCopyHeaderOnRedirect(header.Key, isCrossAuthorityRedirect))
            {
                continue;
            }

            redirectedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return redirectedRequest;
    }

    private static bool HasSameAuthority(Uri sourceUri, Uri destinationUri)
    {
        return string.Equals(sourceUri.Scheme, destinationUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(sourceUri.Host, destinationUri.Host, StringComparison.OrdinalIgnoreCase) &&
               sourceUri.Port == destinationUri.Port;
    }

    private static bool ShouldCopyHeaderOnRedirect(string headerName, bool isCrossAuthorityRedirect)
    {
        if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!isCrossAuthorityRedirect)
        {
            return true;
        }

        return !string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(headerName, "Cookie", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(headerName, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogWellKnownResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = response.Content;
        if (content == null)
        {
            _logger.LogDebug("Well-known response had no content.");
            return;
        }

        var contentLength = content.Headers.ContentLength;
        if (!contentLength.HasValue)
        {
            _logger.LogDebug("Skipping well-known response inspection because Content-Length is unavailable.");
            return;
        }

        if (contentLength.Value > MaxInspectableWellKnownResponseBytes)
        {
            _logger.LogWarning(
                "Skipping well-known response inspection because content length {ContentLength} exceeds the inspection limit of {MaxContentLength} bytes.",
                contentLength.Value,
                MaxInspectableWellKnownResponseBytes);
            return;
        }

        var contentBytes = await content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogDebug("Well-known response length: {Length} bytes", contentBytes.Length);

        try
        {
            using var document = JsonDocument.Parse(contentBytes);
            var root = document.RootElement;

            var jwksUri = TryExtractJwksUri(root);
            if (!string.IsNullOrEmpty(jwksUri))
            {
                _logger.LogDebug("OpenID config points to JWKS URI: {JwksUri}", jwksUri);
                return;
            }

            var kids = new List<string>(MaxKidLogCount);
            var kidCount = ExtractKids(root, kids);
            if (kidCount == 0)
            {
                _logger.LogWarning("JWKS response contains no kids");
                return;
            }

            _logger.LogDebug("JWKS contains {KidCount} keys", kidCount);
            if (kids.Count > 0)
            {
                _logger.LogDebug("JWKS kids (first {Count}): {Kids}", kids.Count, string.Join(", ", kids));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Well-known response is not valid JSON.");
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

        TimeSpan? delay = null;

        if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
        {
            delay = retryAfter.Delta.Value;
        }
        else if (retryAfter.Date.HasValue)
        {
            delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
        }

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            return delay.Value > MaxRetryAfterDelay ? MaxRetryAfterDelay : delay.Value;
        }

        return null;
    }
}
