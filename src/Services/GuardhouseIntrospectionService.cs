namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Extensions;
using Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Service for introspecting tokens with the identity server using a micro-cache strategy.
/// This service should be used by resource servers to validate incoming tokens.
/// </summary>
public class GuardhouseIntrospectionService(
    HttpClient httpClient,
    IMemoryCache memoryCache,
    IOptions<GuardhouseResourceOptions> options,
    ILogger<GuardhouseIntrospectionService>? logger = null) : IGuardhouseIntrospectionService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IOptions<GuardhouseResourceOptions> _options = options;
    private readonly ILogger<GuardhouseIntrospectionService> _logger = logger ?? NullLogger<GuardhouseIntrospectionService>.Instance;

    private const string IntrospectionCacheKeyPrefix = "guardhouse_introspection_";

    /// <summary>
    /// Introspects a token to determine if it is active and retrieve its claims.
    /// Uses a micro-cache strategy to handle burst traffic while maintaining near-real-time revocation security.
    /// </summary>
    /// <param name="token">The access token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response containing the token's status and claims.</returns>
    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var resourceOptions = _options.Value;

        if (string.IsNullOrEmpty(resourceOptions.IntrospectionClientId) ||
            string.IsNullOrEmpty(resourceOptions.IntrospectionClientSecret))
        {
            throw new InvalidOperationException(
                "Introspection is enabled but IntrospectionClientId and IntrospectionClientSecret are not configured. " +
                "Please configure these values in GuardhouseResourceOptions.");
        }

        var cacheKey = $"{IntrospectionCacheKeyPrefix}{GetTokenHash(token)}";
        if (_memoryCache.TryGetValue(cacheKey, out IntrospectionResponse? cachedResult) && cachedResult != null)
        {
            _logger.LogDebugIf(_options.Value.EnableDebug, "Using cached introspection result");
            return cachedResult;
        }

        var authorityUri = EnsureAuthorityUri(resourceOptions.Authority, resourceOptions.RequireHttps);
        var introspectionEndpoint = new Uri(authorityUri, GuardhouseConstants.Endpoints.ConnectIntrospect).ToString();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(GuardhouseConstants.Defaults.RequestTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("token", token),
            new("token_type_hint", "access_token")
        };

        if (resourceOptions.IntrospectionCredentialTransmission == IntrospectionCredentialTransmission.BasicAuth)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{resourceOptions.IntrospectionClientId}:{resourceOptions.IntrospectionClientSecret}")));
        }
        else
        {
            formData.Add(new KeyValuePair<string, string>("client_id", resourceOptions.IntrospectionClientId));
            formData.Add(new KeyValuePair<string, string>("client_secret", resourceOptions.IntrospectionClientSecret));
        }

        request.Content = new FormUrlEncodedContent(formData);

        _logger.LogDebugIf(_options.Value.EnableDebug, "Introspecting token at {IntrospectionEndpoint}", introspectionEndpoint);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, combinedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogError("Introspection request timed out after {TimeoutSeconds} seconds", GuardhouseConstants.Defaults.RequestTimeoutSeconds);
            throw new TimeoutException($"Introspection request timed out after {GuardhouseConstants.Defaults.RequestTimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send introspection request to {IntrospectionEndpoint}", introspectionEndpoint);
            throw new InvalidOperationException(
                $"Failed to send introspection request to {introspectionEndpoint}. " +
                $"Error: {ex.Message}. " +
                $"Verify your Guardhouse instance is accessible and introspection credentials are correct.", ex);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Introspection request failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                responseContent);

            var errorDetails = ParseErrorResponse(responseContent);
            throw new InvalidOperationException(
                $"Failed to introspect token from {introspectionEndpoint}. " +
                $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                $"{errorDetails}. " +
                $"Verify your IntrospectionClientId and IntrospectionClientSecret are configured correctly in GuardhouseResourceOptions.");
        }

        var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize introspection response");

        _logger.LogDebugIf(_options.Value.EnableDebug, "Token introspection completed, active: {Active}", introspectionResponse.Active);

        if (introspectionResponse.Active)
        {
            var cacheTtl = TimeSpan.FromSeconds(resourceOptions.IntrospectionCacheTtlSeconds);
            if (cacheTtl > TimeSpan.Zero)
            {
                _memoryCache.Set(cacheKey, introspectionResponse, cacheTtl);
                _logger.LogDebugIf(_options.Value.EnableDebug, "Cached introspection result for {CacheTtl}", cacheTtl);
            }
        }

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
            _logger.LogError(ex, "Failed to check token activity");
            return false;
        }
    }

    private static string GetTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
            throw new InvalidOperationException("HTTPS is required for introspection endpoints.");
        }

        return uri;
    }
}
