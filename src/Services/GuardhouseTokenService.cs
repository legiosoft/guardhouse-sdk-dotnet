namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;

public class GuardhouseTokenService(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    IOptions<GuardhouseClientOptions> options,
    ILogger<GuardhouseTokenService> logger,
    IClock? clock = null) : IGuardhouseTokenService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IOptions<GuardhouseClientOptions> _options = options;
    private readonly ILogger<GuardhouseTokenService> _logger = logger;
    private readonly IClock _clock = clock ?? SystemClock.Instance;

    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy = options.Value.EnableHttpResilience
        ? Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                options.Value.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        "Request failed with {StatusCode}. Retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                        outcome.Result?.StatusCode,
                        timespan.TotalSeconds,
                        retryAttempt,
                        options.Value.MaxRetryAttempts);
                })
        : Policy.NoOpAsync<HttpResponseMessage>();

    private const string TokenCacheKey = "guardhouse_access_token";
    private const string RefreshTokenCacheKey = "guardhouse_refresh_token";

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (options.EnableTokenCaching && _memoryCache.TryGetValue(TokenCacheKey, out TokenResponse? cachedToken))
        {
            if (cachedToken != null && !cachedToken.IsExpired(options.CacheExpirationBufferSeconds))
            {
                _logger.LogDebug("Using cached access token");
                return cachedToken.AccessToken;
            }

            _logger.LogDebug("Cached token expired, requesting new token");
        }

        if (options.EnableTokenRefresh && _memoryCache.TryGetValue(RefreshTokenCacheKey, out string? cachedRefreshToken))
        {
            try
            {
                var refreshedToken = await RefreshTokenAsync(cachedRefreshToken!, cancellationToken);
                CacheToken(refreshedToken);
                return refreshedToken.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh token, requesting new token");
            }
        }

        var newToken = await RequestTokenAsync(cancellationToken);
        CacheToken(newToken);
        return newToken.AccessToken;
    }

    public async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var tokenEndpoint = $"{options.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectToken}";

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", options.ClientId),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", options.Scope)
        ]);

        _logger.LogDebug("Requesting new token from {TokenEndpoint}", tokenEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) => await _httpClient.SendAsync(request, ct),
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        _logger.LogDebug("Successfully obtained new token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var tokenEndpoint = $"{options.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectToken}";

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", options.ClientId),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        ]);

        _logger.LogDebug("Refreshing token from {TokenEndpoint}", tokenEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) => await _httpClient.SendAsync(request, ct),
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        _logger.LogDebug("Successfully refreshed token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse;
    }

    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var introspectionEndpoint = $"{options.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectIntrospect}";

        using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", options.ClientId),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret),
            new KeyValuePair<string, string>("token", token)
        ]);

        _logger.LogDebug("Introspecting token from {IntrospectionEndpoint}", introspectionEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) => await _httpClient.SendAsync(request, ct),
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize introspection response");

        _logger.LogDebug("Token introspection completed, active: {Active}", introspectionResponse.Active);

        return introspectionResponse;
    }

    public async Task<bool> IsTokenActiveAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var introspectionResult = await IntrospectTokenAsync(token, cancellationToken);
            return introspectionResult.Active;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check token activity");
            return false;
        }
    }

    private void CacheToken(TokenResponse tokenResponse)
    {
        var options = _options.Value;

        if (options.EnableTokenCaching)
        {
            var cacheExpiration = Duration.FromSeconds(tokenResponse.ExpiresIn - options.CacheExpirationBufferSeconds);
            if (cacheExpiration > Duration.Zero)
            {
                var expirationInstant = _clock.GetCurrentInstant().Plus(cacheExpiration);
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheExpiration.ToTimeSpan()
                };

                _memoryCache.Set(TokenCacheKey, tokenResponse, cacheOptions);
                _logger.LogDebug("Cached access token until {ExpirationInstant}", expirationInstant);
            }

            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                var refreshCacheExpiration = Duration.FromDays(30);
                var refreshCacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = refreshCacheExpiration.ToTimeSpan()
                };

                _memoryCache.Set(RefreshTokenCacheKey, tokenResponse.RefreshToken, refreshCacheOptions);
                _logger.LogDebug("Cached refresh token for {RefreshCacheExpiration}", refreshCacheExpiration);
            }
        }
    }
}
