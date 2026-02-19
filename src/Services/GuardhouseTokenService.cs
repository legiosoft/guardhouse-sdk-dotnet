namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Extensions;
using Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;

/// <summary>
/// Service for obtaining and managing access tokens from the identity server.
/// Handles token caching, refresh, and retry logic automatically.
/// </summary>
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

    private string GetTokenCacheKey()
    {
        return $"guardhouse_access_token_{options.Value.ClientId}";
    }

    private string GetRefreshTokenCacheKey()
    {
        return $"guardhouse_refresh_token_{options.Value.ClientId}";
    }

    /// <summary>
    /// Gets an access token, using cached tokens or requesting a new one as needed.
    /// This method automatically handles token caching, refresh, and retry logic.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A valid access token.</returns>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;

        if (options1.EnableTokenCaching && memoryCache.TryGetValue(GetTokenCacheKey(), out TokenResponse? cachedToken))
        {
            if (cachedToken != null && !cachedToken.IsExpired(options1.CacheExpirationBufferSeconds))
            {
                logger.LogDebugIf(options1.EnableDebug, "Using cached access token");
                return cachedToken.AccessToken;
            }

            logger.LogDebugIf(options1.EnableDebug, "Cached token expired, requesting new token");
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (options1.EnableTokenCaching && memoryCache.TryGetValue(GetTokenCacheKey(), out cachedToken))
            {
                if (cachedToken != null && !cachedToken.IsExpired(options1.CacheExpirationBufferSeconds))
                {
                    logger.LogDebugIf(options1.EnableDebug, "Using cached access token (double-checked)");
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

    /// <summary>
    /// Requests a new access token from the identity server using client credentials grant.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A token response containing the access token and related information.</returns>
    public async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var authorityUri = EnsureAuthorityUri(options1.Authority, options1.RequireHttps);
        var tokenEndpoint = new Uri(authorityUri, GuardhouseConstants.Endpoints.ConnectToken).ToString();

        logger.LogDebugIf(options1.EnableDebug, "Requesting new token from {TokenEndpoint} for client {ClientId}", tokenEndpoint, options1.ClientId);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(
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
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send token request to {TokenEndpoint}", tokenEndpoint);
            throw new InvalidOperationException(
                $"Failed to send token request to {tokenEndpoint}. " +
                $"Check your Guardhouse configuration in appsettings.json. " +
                $"Authority: {options1.Authority}, ClientId: {options1.ClientId}. " +
                $"Error: {ex.Message}", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Token request failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseContent);

            var errorDetails = ParseErrorResponse(responseContent);
            throw new InvalidOperationException(
                $"Failed to request token from {tokenEndpoint}. " +
                $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                $"{errorDetails}. " +
                $"Please verify your Guardhouse credentials in appsettings.json. " +
                $"Authority: {options1.Authority}, ClientId: {options1.ClientId}, Scope: {options1.Scope}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        logger.LogDebugIf(options1.EnableDebug, "Successfully obtained new token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse.WithClock(_clock);
    }

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use for obtaining a new access token.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A token response containing the new access token and related information.</returns>
    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var authorityUri = EnsureAuthorityUri(options1.Authority, options1.RequireHttps);
        var tokenEndpoint = new Uri(authorityUri, GuardhouseConstants.Endpoints.ConnectToken).ToString();

        logger.LogDebugIf(options1.EnableDebug, "Refreshing token from {TokenEndpoint} for client {ClientId}", tokenEndpoint, options1.ClientId);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(
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
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send token refresh request to {TokenEndpoint}", tokenEndpoint);
            throw new InvalidOperationException(
                $"Failed to send token refresh request to {tokenEndpoint}. " +
                $"Error: {ex.Message}", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Token refresh failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseContent);

            var errorDetails = ParseErrorResponse(responseContent);
            throw new InvalidOperationException(
                $"Failed to refresh token from {tokenEndpoint}. " +
                $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                $"{errorDetails}. " +
                $"Your refresh token may have expired.");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        logger.LogDebugIf(options1.EnableDebug, "Successfully refreshed token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return tokenResponse.WithClock(_clock);
    }

    /// <summary>
    /// Introspects a token to determine if it is active and retrieve its claims.
    /// Uses client credentials for authentication to the introspection endpoint.
    /// </summary>
    /// <param name="token">The token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response containing the token's status and claims.</returns>
    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var options1 = options.Value;
        var authorityUri = EnsureAuthorityUri(options1.Authority, options1.RequireHttps);
        var introspectionEndpoint = new Uri(authorityUri, GuardhouseConstants.Endpoints.ConnectIntrospect).ToString();

        logger.LogDebugIf(options1.EnableDebug, "Introspecting token from {IntrospectionEndpoint}", introspectionEndpoint);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options1.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(
                async (ct) =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);

                    var clientId = options1.IntrospectionClientId ?? options1.ClientId;
                    var clientSecret = options1.IntrospectionClientSecret ?? options1.ClientSecret;

                    var formData = new List<KeyValuePair<string, string>>
                    {
                        new("token", token)
                    };

                    if (options1.IntrospectionCredentialTransmission == IntrospectionCredentialTransmission.BasicAuth)
                    {
                        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                    }
                    else
                    {
                        formData.Add(new KeyValuePair<string, string>("client_id", clientId));
                        formData.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
                    }

                    request.Content = new FormUrlEncodedContent(formData);
                    return await httpClient.SendAsync(request, ct);
                },
                combinedCts.Token);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send introspection request to {IntrospectionEndpoint}", introspectionEndpoint);
            throw new InvalidOperationException(
                $"Failed to send introspection request to {introspectionEndpoint}. " +
                $"Error: {ex.Message}. " +
                $"Verify your Guardhouse instance is accessible and credentials are correct. " +
                $"For introspection, you may need to configure IntrospectionClientId and IntrospectionClientSecret in your GuardhouseClientOptions.", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Introspection request failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseContent);

            var errorDetails = ParseErrorResponse(responseContent);
            throw new InvalidOperationException(
                $"Failed to introspect token from {introspectionEndpoint}. " +
                $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                $"{errorDetails}. " +
                $"Verify your ClientId and ClientSecret are configured correctly.");
        }

        var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize introspection response");

        logger.LogDebugIf(options1.EnableDebug, "Token introspection completed, active: {Active}", introspectionResponse.Active);

        return introspectionResponse;
    }

    /// <summary>
    /// Checks if a token is currently active.
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the token is active, false otherwise.</returns>
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
                logger.LogDebugIf(options1.EnableDebug, "Cached access token until {ExpirationInstant}", expirationInstant);
            }

            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                var refreshCacheExpiration = Duration.FromDays(30);
                var refreshCacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = refreshCacheExpiration.ToTimeSpan()
                };

                memoryCache.Set(GetRefreshTokenCacheKey(), tokenResponse.RefreshToken, refreshCacheOptions);
                logger.LogDebugIf(options1.EnableDebug, "Cached refresh token for {RefreshCacheExpiration}", refreshCacheExpiration);
            }
        }
    }

    private static string ParseErrorResponse(string responseContent)
    {
        try
        {
            var errorDoc = JsonDocument.Parse(responseContent);
            var error = errorDoc.RootElement.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null;
            var errorDescription = errorDoc.RootElement.TryGetProperty("error_description", out var descProp) ? descProp.GetString() : null;

            if (!string.IsNullOrEmpty(error))
            {
                if (!string.IsNullOrEmpty(errorDescription))
                {
                    return $"Error: {error}. Description: {errorDescription}";
                }
                return $"Error: {error}";
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return $"Response: {responseContent}";
    }

    private static Uri EnsureAuthorityUri(string authority, bool requireHttps)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Authority must be an absolute URI.");
        }

        if (requireHttps && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for token and introspection endpoints.");
        }

        return uri;
    }
}
