namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    private readonly IClock _clock = clock ?? SystemClock.Instance;

    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy = options.Value.EnableHttpResilience
        ? Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                options.Value.MaxRetryAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryAttempt, _) =>
                {
                    logger.LogWarning(
                        "Request failed with {StatusCode}. Retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                        outcome.Result?.StatusCode,
                        timespan.TotalSeconds,
                        retryAttempt,
                        options.Value.MaxRetryAttempts);
                })
        : Policy.NoOpAsync<HttpResponseMessage>();

    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private string GetTokenCacheKey() => $"guardhouse_access_token_{options.Value.ClientId}";
    private string GetRefreshTokenCacheKey() => $"guardhouse_refresh_token_{options.Value.ClientId}";

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;

        if (options1.EnableTokenCaching && memoryCache.TryGetValue(GetTokenCacheKey(), out TokenResponse? cachedToken))
        {
            if (cachedToken != null && !cachedToken.IsExpired(options1.CacheExpirationBufferSeconds))
            {
                logger.LogDebug("Using cached access token");
                return cachedToken.AccessToken;
            }

            logger.LogDebug("Cached token expired, requesting new token");
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (options1.EnableTokenCaching && memoryCache.TryGetValue(GetTokenCacheKey(), out cachedToken))
            {
                if (cachedToken != null && !cachedToken.IsExpired(options1.CacheExpirationBufferSeconds))
                {
                    logger.LogDebug("Using cached access token (double-checked)");
                    return cachedToken.AccessToken;
                }
            }

            if (options1.EnableTokenRefresh && memoryCache.TryGetValue(GetRefreshTokenCacheKey(), out string? cachedRefreshToken))
            {
                try
                {
                    var refreshedToken = await RefreshTokenAsync(cachedRefreshToken!, cancellationToken);
                    CacheToken(refreshedToken);
                    return refreshedToken.AccessToken;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to refresh token, requesting new token");
                }
            }

            var newToken = await RequestTokenAsync(cancellationToken);
            CacheToken(newToken);
            return newToken.AccessToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    public async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var tokenEndpoint = $"{options1.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectToken}";

        logger.LogDebug("Requesting new token from {TokenEndpoint}", tokenEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("client_id", options1.ClientId),
                    new KeyValuePair<string, string>("client_secret", options1.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", options1.Scope)
                ]);
                return await httpClient.SendAsync(request, ct);
            },
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        logger.LogDebug("Successfully obtained new token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var tokenEndpoint = $"{options1.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectToken}";

        logger.LogDebug("Refreshing token from {TokenEndpoint}", tokenEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("client_id", options1.ClientId),
                    new KeyValuePair<string, string>("client_secret", options1.ClientSecret),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                ]);
                return await httpClient.SendAsync(request, ct);
            },
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        logger.LogDebug("Successfully refreshed token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse;
    }

    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var introspectionEndpoint = $"{options1.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectIntrospect}";

        logger.LogDebug("Introspecting token from {IntrospectionEndpoint}", introspectionEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await _retryPolicy.ExecuteAsync(
            async (ct) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);
                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("token", token)
                ]);
                var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options1.ClientId}:{options1.ClientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                return await httpClient.SendAsync(request, ct);
            },
            combinedCts.Token);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize introspection response");

        logger.LogDebug("Token introspection completed, active: {Active}", introspectionResponse.Active);

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
            logger.LogError(ex, "Failed to check token activity");
            return false;
        }
    }

    private void CacheToken(TokenResponse tokenResponse)
    {
        var options1 = options.Value;

        if (options1.EnableTokenCaching)
        {
            var cacheExpiration = Duration.FromSeconds(tokenResponse.ExpiresIn - options1.CacheExpirationBufferSeconds);
            if (cacheExpiration > Duration.Zero)
            {
                var expirationInstant = _clock.GetCurrentInstant().Plus(cacheExpiration);
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheExpiration.ToTimeSpan()
                };

                memoryCache.Set(GetTokenCacheKey(), tokenResponse, cacheOptions);
                logger.LogDebug("Cached access token until {ExpirationInstant}", expirationInstant);
            }

            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                var refreshCacheExpiration = Duration.FromDays(30);
                var refreshCacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = refreshCacheExpiration.ToTimeSpan()
                };

                memoryCache.Set(GetRefreshTokenCacheKey(), tokenResponse.RefreshToken, refreshCacheOptions);
                logger.LogDebug("Cached refresh token for {RefreshCacheExpiration}", refreshCacheExpiration);
            }
        }
    }
}
