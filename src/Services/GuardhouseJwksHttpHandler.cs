namespace Guardhouse.SDK.Services;

using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

internal class GuardhouseJwksHttpHandler(
    HttpMessageHandler innerHandler,
    ILogger<GuardhouseJwksHttpHandler> logger,
    int maxRetryAttempts) : DelegatingHandler(innerHandler)
{
    private const int MaxKidLogCount = 10;

    private readonly ILogger<GuardhouseJwksHttpHandler> _logger = logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy = BuildRetryPolicy(logger, maxRetryAttempts);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = request.RequestUri?.ToString();
        var isWellKnownRequest = IsWellKnownRequest(request.RequestUri);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Backchannel request: {Method} {Url}", request.Method, requestUrl);
        }

        try
        {
            var response = isWellKnownRequest
                ? await _retryPolicy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken)
                : await base.SendAsync(request, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Backchannel response: {StatusCode} from {Url}", (int)response.StatusCode, requestUrl);
            }

            if (isWellKnownRequest && response.IsSuccessStatusCode && _logger.IsEnabled(LogLevel.Information))
            {
                await LogWellKnownResponseAsync(response, cancellationToken);
            }

            return response;
        }
        catch (Exception ex)
        {
            if (isWellKnownRequest)
            {
                _logger.LogError(ex, "JWKS request failed: {Url}", requestUrl);
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
        if (response.Content == null)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Well-known response length: {Length} chars", content.Length);

        var jwksUri = TryExtractJwksUri(content);
        if (!string.IsNullOrEmpty(jwksUri))
        {
            _logger.LogInformation("OpenID config points to JWKS URI: {JwksUri}", jwksUri);
            return;
        }

        var kidMatches = Regex.Matches(content, "\\\"kid\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        if (kidMatches.Count == 0)
        {
            _logger.LogWarning("JWKS response contains no kids");
            return;
        }

        _logger.LogInformation("JWKS contains {KidCount} keys", kidMatches.Count);
        var kids = kidMatches.Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .Take(MaxKidLogCount)
            .ToArray();

        if (kids.Length > 0)
        {
            _logger.LogInformation("JWKS kids (first {Count}): {Kids}", kids.Length, string.Join(", ", kids));
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
