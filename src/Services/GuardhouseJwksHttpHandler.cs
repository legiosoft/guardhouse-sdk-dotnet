namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
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

            if (isWellKnownRequest && response.IsSuccessStatusCode)
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
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "Well-known response length: {Length} chars", content.Length);

        var jwksUri = TryExtractJwksUri(content);
        if (!string.IsNullOrEmpty(jwksUri))
        {
            logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "OpenID config points to JWKS URI: {JwksUri}", jwksUri);
            return;
        }

        var kidMatches = Regex.Matches(content, "\\\"kid\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        if (kidMatches.Count == 0)
        {
            logger.LogWarning("JWKS response contains no kids");
            return;
        }

        logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "JWKS contains {KidCount} keys", kidMatches.Count);
        var kids = kidMatches.Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .Take(MaxKidLogCount)
            .ToArray();

        if (kids.Length > 0)
        {
            logger.LogDebugIf(logger.IsEnabled(LogLevel.Debug), "JWKS kids (first {Count}): {Kids}", kids.Length, string.Join(", ", kids));
        }
    }

    private static string? TryExtractJwksUri(string content)
    {
        var match = Regex.Match(content, "\\\"jwks_uri\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        return match.Success ? match.Groups[1].Value : null;
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
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                (result, timeSpan, retryCount, _) =>
                {
                    logger.LogWarning(
                        "JWKS request failed, retrying in {Delay}s. Attempt {Attempt}/{MaxAttempts}. Status: {StatusCode}",
                        timeSpan.TotalSeconds, retryCount, maxRetryAttempts, result.Result?.StatusCode);
                });
    }
}
