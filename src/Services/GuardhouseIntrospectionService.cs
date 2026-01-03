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
using Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Service for introspecting tokens with the identity server using a micro-cache strategy.
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
        var resourceOptions = _options.Value;

        if (string.IsNullOrEmpty(resourceOptions.IntrospectionClientId) ||
            string.IsNullOrEmpty(resourceOptions.IntrospectionClientSecret))
        {
            throw new InvalidOperationException("Introspection is enabled but " +
                                                "IntrospectionClientId and IntrospectionClientSecret " +
                                                "are not configured.");
        }

        var cacheKey = $"{IntrospectionCacheKeyPrefix}{GetTokenHash(token)}";
        if (_memoryCache.TryGetValue(cacheKey, out IntrospectionResponse? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Using cached introspection result");
            return cachedResult;
        }

        var introspectionEndpoint = $"{resourceOptions.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectIntrospect}";

        using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{resourceOptions.IntrospectionClientId}:{resourceOptions.IntrospectionClientSecret}")));

        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("token", token),
            new KeyValuePair<string, string>("token_type_hint", "access_token")
        ]);

        _logger.LogDebug("Introspecting token at {IntrospectionEndpoint}", introspectionEndpoint);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var introspectionResponse = JsonSerializer.Deserialize<IntrospectionResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize introspection response");

        _logger.LogDebug("Token introspection completed, active: {Active}", introspectionResponse.Active);

        if (introspectionResponse.Active)
        {
            var cacheTtl = TimeSpan.FromSeconds(resourceOptions.IntrospectionCacheTtlSeconds);
            if (cacheTtl > TimeSpan.Zero)
            {
                _memoryCache.Set(cacheKey, introspectionResponse, cacheTtl);
                _logger.LogDebug("Cached introspection result for {CacheTtl}", cacheTtl);
            }
        }

        return introspectionResponse;
    }

    private static string GetTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
