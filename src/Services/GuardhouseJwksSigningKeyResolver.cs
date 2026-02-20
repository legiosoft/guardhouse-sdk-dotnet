namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Models;

internal sealed class GuardhouseJwksSigningKeyResolver
{
    private const int MaxKidLogCount = 10;

    private readonly GuardhouseResourceOptions _resourceOptions;
    private readonly JwtBearerOptions _jwtOptions;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _cacheDuration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CachedKeySet? _cachedKeys;

    public GuardhouseJwksSigningKeyResolver(
        GuardhouseResourceOptions resourceOptions,
        JwtBearerOptions jwtOptions,
        Uri authorityUri,
        ILoggerFactory loggerFactory)
    {
        _resourceOptions = resourceOptions;
        _jwtOptions = jwtOptions;
        _logger = loggerFactory.CreateLogger<GuardhouseJwksSigningKeyResolver>();

        var allowedHosts = BuildAllowedHosts(authorityUri, resourceOptions.JwksAllowedHosts);
        var jwksHandler = new GuardhouseJwksHttpHandler(
            new HttpClientHandler(),
            loggerFactory.CreateLogger<GuardhouseJwksHttpHandler>(),
            Math.Max(resourceOptions.MaxRetryAttempts, 0),
            allowedHosts,
            resourceOptions.RequireHttps);

        _httpClient = new HttpClient(jwksHandler)
        {
            Timeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(resourceOptions.RequestTimeoutSeconds))
        };

        _cacheDuration = BuildCacheDuration(resourceOptions);
    }

    public async Task WarmupAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!forceRefresh && TryGetCachedKeys(null)?.Length > 0)
        {
            return;
        }

        var lockTaken = false;
        try
        {
            var timeout = forceRefresh ? TimeSpan.Zero : Timeout.InfiniteTimeSpan;
            lockTaken = await _refreshLock.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (!lockTaken)
            {
                return;
            }

            var cached = _cachedKeys;
            if (!forceRefresh && cached != null && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return;
            }

            var configuration = await TryGetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var configKeys = configuration?.SigningKeys?.ToArray() ?? [];
            if (configKeys.Length > 0)
            {
                UpdateCache(configKeys);
                return;
            }

            var jwksUri = configuration?.JwksUri ?? BuildJwksUri();
            if (string.IsNullOrWhiteSpace(jwksUri))
            {
                _logger.LogWarning("JWKS URI could not be resolved.");
                return;
            }

            var keys = await FetchKeysAsync(jwksUri, cancellationToken).ConfigureAwait(false);
            if (keys.Length > 0)
            {
                UpdateCache(keys);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWKS warmup failed.");
        }
        finally
        {
            if (lockTaken)
            {
                _refreshLock.Release();
            }
        }
    }

    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        _ = token;
        _ = securityToken;
        _ = validationParameters;

        var configuredKeys = GetConfiguredKeys(validationParameters, kid);
        if (configuredKeys.Length > 0)
        {
            return configuredKeys;
        }

        var cachedKeys = TryGetCachedKeys(kid);
        if (cachedKeys != null && cachedKeys.Length > 0)
        {
            return cachedKeys;
        }

        if (!string.IsNullOrWhiteSpace(kid))
        {
            _jwtOptions.ConfigurationManager?.RequestRefresh();
            _ = WarmupAsync(CancellationToken.None, forceRefresh: true);
        }

        return [];
    }

    private SecurityKey[]? TryGetCachedKeys(string? kid)
    {
        var cached = _cachedKeys;
        if (cached == null || cached.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(kid) || cached.Kids.Contains(kid))
        {
            return cached.Keys;
        }

        return null;
    }


    private async Task<OpenIdConnectConfiguration?> TryGetConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_jwtOptions.ConfigurationManager == null)
        {
            return null;
        }

        try
        {
            return await _jwtOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenID configuration retrieval failed.");
            return null;
        }
    }

    private string? BuildJwksUri()
    {
        var authority = NormalizeAuthority(_jwtOptions.Authority ?? _resourceOptions.Authority);
        return string.IsNullOrWhiteSpace(authority)
            ? null
            : $"{authority}/{GuardhouseConstants.Endpoints.WellKnownJwks}";
    }

    private async Task<SecurityKey[]> FetchKeysAsync(string jwksUri, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(jwksUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("JWKS request failed with status {StatusCode} from {JwksUri}",
                    (int)response.StatusCode, jwksUri);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("JWKS response was empty from {JwksUri}", jwksUri);
                return [];
            }

            var jwks = new JsonWebKeySet(json);
            var keys = jwks.Keys?.Cast<SecurityKey>().ToArray() ?? [];
            LogKeySummary(keys, jwksUri);
            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWKS request failed from {JwksUri}", jwksUri);
            return [];
        }
    }

    private void LogKeySummary(IReadOnlyCollection<SecurityKey> keys, string jwksUri)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        if (keys.Count == 0)
        {
            _logger.LogDebug("JWKS contains no keys from {JwksUri}", jwksUri);
            return;
        }

        _logger.LogDebug("JWKS contains {KeyCount} keys from {JwksUri}", keys.Count, jwksUri);
        var kids = new List<string>(MaxKidLogCount);
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key.KeyId))
            {
                kids.Add(key.KeyId);
                if (kids.Count >= MaxKidLogCount)
                {
                    break;
                }
            }
        }

        if (kids.Count > 0)
        {
            _logger.LogDebug("JWKS kids (first {Count}): {Kids}", kids.Count, string.Join(", ", kids));
        }
    }

    private static SecurityKey[] GetConfiguredKeys(TokenValidationParameters validationParameters, string? kid)
    {
        var keys = new List<SecurityKey>();
        if (validationParameters.IssuerSigningKey != null)
        {
            keys.Add(validationParameters.IssuerSigningKey);
        }

        if (validationParameters.IssuerSigningKeys != null)
        {
            keys.AddRange(validationParameters.IssuerSigningKeys);
        }

        if (keys.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(kid))
        {
            return keys.ToArray();
        }

        return keys.Where(key => string.Equals(key.KeyId, kid, StringComparison.Ordinal)).ToArray();
    }


    private static HashSet<string> BuildKidSet(IEnumerable<SecurityKey> keys)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key.KeyId))
            {
                set.Add(key.KeyId);
            }
        }

        return set;
    }

    private void UpdateCache(SecurityKey[] keys)
    {
        _cachedKeys = new CachedKeySet(keys, BuildKidSet(keys), DateTimeOffset.UtcNow.Add(_cacheDuration));
    }

    private static HashSet<string> BuildAllowedHosts(Uri authorityUri, string[]? extraHosts)
    {
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            authorityUri.Host
        };

        if (extraHosts == null)
        {
            return allowedHosts;
        }

        foreach (var host in extraHosts)
        {
            if (!string.IsNullOrWhiteSpace(host))
            {
                allowedHosts.Add(host.Trim());
            }
        }

        return allowedHosts;
    }

    private static string NormalizeAuthority(string authority)
    {
        return string.IsNullOrWhiteSpace(authority) ? string.Empty : authority.TrimEnd('/');
    }

    private static TimeSpan BuildCacheDuration(GuardhouseResourceOptions options)
    {
        var cacheMinutes = GetCacheDurationHours(options.JwksCacheDurationHours) * 60;
        var refreshMinutes = GetRefreshIntervalMinutes(options.JwksRefreshIntervalMinutes);
        var effectiveMinutes = Math.Min(cacheMinutes, refreshMinutes);
        return TimeSpan.FromMinutes(effectiveMinutes);
    }

    private static int GetCacheDurationHours(int cacheDurationHours)
    {
        return cacheDurationHours > 0
            ? cacheDurationHours
            : GuardhouseConstants.Defaults.JwksCacheDurationHours;
    }

    private static int GetRefreshIntervalMinutes(int refreshIntervalMinutes)
    {
        return refreshIntervalMinutes > 0
            ? refreshIntervalMinutes
            : GuardhouseConstants.Defaults.JwksRefreshIntervalMinutes;
    }

    private static int GetRequestTimeoutSeconds(int requestTimeoutSeconds)
    {
        return requestTimeoutSeconds > 0
            ? requestTimeoutSeconds
            : GuardhouseConstants.Defaults.RequestTimeoutSeconds;
    }

    private sealed record CachedKeySet(SecurityKey[] Keys, HashSet<string> Kids, DateTimeOffset ExpiresAt);
}
