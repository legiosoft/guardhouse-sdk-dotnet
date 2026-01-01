using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Constants;

namespace Guardhouse.SDK.Services;

internal class GuardhouseIntrospectionService : IGuardhouseIntrospectionService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<GuardhouseResourceOptions> _options;
    private readonly ILogger<GuardhouseIntrospectionService> _logger;

    private const string IntrospectionCacheKeyPrefix = "guardhouse_introspection_";

    public GuardhouseIntrospectionService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<GuardhouseResourceOptions> options,
        ILogger<GuardhouseIntrospectionService>? logger = null)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _options = options;
        _logger = logger ?? NullLogger<GuardhouseIntrospectionService>.Instance;
    }

    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var resourceOptions = _options.Value;

        if (string.IsNullOrEmpty(resourceOptions.IntrospectionClientId) || 
            string.IsNullOrEmpty(resourceOptions.IntrospectionClientSecret))
        {
            throw new InvalidOperationException("Introspection is enabled but IntrospectionClientId and IntrospectionClientSecret are not configured.");
        }

        var cacheKey = $"{IntrospectionCacheKeyPrefix}{token}";
        if (_memoryCache.TryGetValue(cacheKey, out IntrospectionResponse? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("Using cached introspection result");
            return cachedResult;
        }

        var introspectionEndpoint = $"{resourceOptions.Authority.TrimEnd('/')}/{GuardhouseConstants.Endpoints.ConnectIntrospect}";

        using var request = new HttpRequestMessage(HttpMethod.Post, introspectionEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", 
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(
                $"{resourceOptions.IntrospectionClientId}:{resourceOptions.IntrospectionClientSecret}")));

        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", token)
        });

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
            var cacheExpiration = introspectionResponse.ExpiresAt.HasValue
                ? introspectionResponse.ExpiresAt.Value.Minus(SystemClock.Instance.GetCurrentInstant()).ToTimeSpan()
                : TimeSpan.FromMinutes(5);

            if (cacheExpiration > TimeSpan.Zero)
            {
                _memoryCache.Set(cacheKey, introspectionResponse, cacheExpiration);
                _logger.LogDebug("Cached introspection result for {CacheExpiration}", cacheExpiration);
            }
        }

        return introspectionResponse;
    }
}
