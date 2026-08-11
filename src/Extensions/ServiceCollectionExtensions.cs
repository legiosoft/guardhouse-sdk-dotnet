// ReSharper disable RedundantNameQualifier

namespace Guardhouse.SDK.Extensions;

using System;
using System.Net.Http;
using System.Linq;
using Constants;
using Models;
using Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;
using Polly.Timeout;

/// <summary>
/// Extension methods for configuring Guardhouse services in dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly IAsyncPolicy<HttpResponseMessage> NoRetryPolicy =
        Policy.NoOpAsync<HttpResponseMessage>();

    /// <summary>
    /// Adds Guardhouse client services to dependency injection container.
    /// This enables your application to obtain access tokens from identity server.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse client options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClient(
        this IServiceCollection services,
        Action<GuardhouseClientOptions>? configureAction = null)
    {
        if (configureAction is not null)
        {
            services.Configure(configureAction);
        }

        if (!IsRegistered<GuardhouseClientRegistrationsMarker>(services))
        {
            services.AddOptions<GuardhouseClientOptions>()
                .ValidateDataAnnotations()
                .Validate(
                    IsOfflineAccessScopeValid,
                    "EnableTokenRefresh requires the offline_access scope. Add offline_access to Scope or set IncludeOfflineAccessScope = true.")
                .ValidateOnStart();

            services.TryAddSingleton<IClock>(SystemClock.Instance);
            services.AddMemoryCache();
            services.TryAddSingleton<GuardhouseClientPolicyCache>();

            // AddHttpClient already registers the service - no need for AddScoped
            services.AddHttpClient<IGuardhouseTokenService, GuardhouseTokenService>((_, client) =>
                {
                    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                })
                .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseClientPolicyCache>().TimeoutPolicy);

            services.TryAddSingleton<GuardhouseClientRegistrationsMarker>();
        }

        return services;
    }

    /// <summary>
    /// Adds Guardhouse resource server services to dependency injection container.
    /// This enables your application to validate incoming JWT tokens.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureAction">Optional action to configure Guardhouse resource options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseResource(
        this IServiceCollection services,
        Action<GuardhouseResourceOptions>? configureAction = null)
    {
        if (configureAction is not null)
        {
            services.Configure(configureAction);
        }

        if (!IsRegistered<GuardhouseResourceRegistrationsMarker>(services))
        {
            services.AddOptions<GuardhouseResourceOptions>()
                .ValidateDataAnnotations()
                .Validate(options =>
                    {
                        if (string.IsNullOrWhiteSpace(options.Authority))
                        {
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(options.Audience))
                        {
                            return false;
                        }

                        if (options.EnableIntrospection)
                        {
                            if (string.IsNullOrWhiteSpace(options.IntrospectionClientId))
                            {
                                return false;
                            }

                            if (string.IsNullOrWhiteSpace(options.IntrospectionClientSecret))
                            {
                                return false;
                            }
                        }

                        return true;
                    },
                    "Guardhouse resource configuration is invalid. Ensure Authority, " +
                    "Audience are set, and when using Introspection mode, " +
                    "IntrospectionClientId and IntrospectionClientSecret are also configured.")
                .ValidateOnStart();

            services.AddMemoryCache();
            services.TryAddSingleton<GuardhouseResourcePolicyCache>();

            // Add the Guardhouse scheme without overriding the default
            services.AddAuthentication()
                .AddJwtBearer(GuardhouseConstants.Authentication.DefaultScheme, _ => { });

            // AddHttpClient already registers the service - no need for AddScoped
            services.AddHttpClient<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>((_, client) =>
                {
                    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                })
                .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseResourcePolicyCache>().RetryPolicy)
                .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseResourcePolicyCache>().TimeoutPolicy);

            // Required for opaque token introspection - registered here so options.EventsType can resolve it
            services.TryAddScoped<GuardhouseIntrospectionJwtBearerEvents>();
            services.TryAddScoped<IGuardhouseResourceService, GuardhouseResourceService>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, ConfigureGuardhouseJwtOptions>());
            services.TryAddSingleton<GuardhouseResourceRegistrationsMarker>();
        }

        return services;
    }

    /// <summary>
    /// Adds both Guardhouse client and resource server services to dependency injection container.
    /// This is a convenience method that calls both AddGuardhouseClient and AddGuardhouseResource.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureClientAction">Optional action to configure Guardhouse client options.</param>
    /// <param name="configureResourceAction">Optional action to configure Guardhouse resource options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouse(
        this IServiceCollection services,
        Action<GuardhouseClientOptions>? configureClientAction = null,
        Action<GuardhouseResourceOptions>? configureResourceAction = null)
    {
        services.AddGuardhouseClient(configureClientAction);
        services.AddGuardhouseResource(configureResourceAction);
        return services;
    }

    /// <summary>
    /// Adds Guardhouse client services with simple configuration using individual parameters.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="authority">The authority URL of identity server.</param>
    /// <param name="clientId">The client ID assigned to your application.</param>
    /// <param name="clientSecret">The client secret for your application.</param>
    /// <param name="scope">The scope(s) to request (default: "api").</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClient(
        this IServiceCollection services,
        string authority,
        string clientId,
        string clientSecret,
        string scope = GuardhouseConstants.Defaults.DefaultScope)
    {
        return services.AddGuardhouseClient(options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.Scope = scope;
            options.EnableTokenRefresh = HasScope(scope, GuardhouseConstants.Scopes.OfflineAccess);
        });
    }

    /// <summary>
    /// Adds Guardhouse resource server services with simple configuration using individual parameters.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="authority">The authority URL of identity server.</param>
    /// <param name="audience">The audience that your resource server expects.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseResource(
        this IServiceCollection services,
        string authority,
        string audience)
    {
        return services.AddGuardhouseResource(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
        });
    }

    /// <summary>
    /// Adds Guardhouse external API clients to dependency injection container.
    /// This enables your application to call the users, roles, and permissions endpoints exposed by Guardhouse.
    /// Requires AddGuardhouseClient to be called first.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse API options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseApiClients(
        this IServiceCollection services,
        Action<GuardhouseUserOptions>? configureAction = null)
    {
        if (configureAction is not null)
        {
            services.Configure(configureAction);
        }

        if (!IsRegistered<GuardhouseApiClientRegistrationsMarker>(services))
        {
            services.AddOptions<GuardhouseUserOptions>()
                .Validate(options =>
                        string.IsNullOrWhiteSpace(options.ApiBaseUrl) ||
                        IsValidApiBaseUrl(options.ApiBaseUrl),
                    "ApiBaseUrl must be an absolute URI when provided.")
                .ValidateOnStart();

            services.TryAddSingleton<GuardhouseClientPolicyCache>();

            AddGuardhouseApiHttpClient<IGuardhouseUsersClient, GuardhouseUsersClient>(services);
            AddGuardhouseApiHttpClient<IGuardhouseRolesClient, GuardhouseRolesClient>(services);
            AddGuardhouseApiHttpClient<IGuardhousePermissionsClient, GuardhousePermissionsClient>(services);
            AddGuardhouseApiHttpClient<IGuardhouseUserService, GuardhouseUserService>(services);
            services.TryAddSingleton<GuardhouseApiClientRegistrationsMarker>();
        }

        return services;
    }

    /// <summary>
    /// Adds Guardhouse external API clients to dependency injection container.
    /// This method is kept as a backward-compatible alias for AddGuardhouseApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse API options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseUserClients(
        this IServiceCollection services,
        Action<GuardhouseUserOptions>? configureAction = null)
    {
        return services.AddGuardhouseApiClients(configureAction);
    }

    /// <summary>
    /// Adds Guardhouse users API services to dependency injection container.
    /// This method is kept as a backward-compatible alias for AddGuardhouseApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse API options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseUserService(
        this IServiceCollection services,
        Action<GuardhouseUserOptions>? configureAction = null)
    {
        return services.AddGuardhouseApiClients(configureAction);
    }

    /// <summary>
    /// Adds Guardhouse external API clients with simple configuration.
    /// Requires AddGuardhouseClient to be called first.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="apiBaseUrl">The base URL of the Guardhouse API (e.g., "https://auth.example.com").</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseApiClients(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        return services.AddGuardhouseApiClients(options =>
        {
            options.ApiBaseUrl = apiBaseUrl;
        });
    }

    /// <summary>
    /// Adds Guardhouse external API clients with simple configuration.
    /// This method is kept as a backward-compatible alias for AddGuardhouseApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="apiBaseUrl">The base URL of the Guardhouse API (e.g., "https://auth.example.com").</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseUserClients(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        return services.AddGuardhouseApiClients(apiBaseUrl);
    }

    /// <summary>
    /// Adds Guardhouse users API services with simple configuration.
    /// This method is kept as a backward-compatible alias for AddGuardhouseApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="apiBaseUrl">The base URL of the Guardhouse API (e.g., "https://auth.example.com").</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseUserService(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        return services.AddGuardhouseApiClients(apiBaseUrl);
    }

    /// <summary>
    /// Adds Guardhouse client and external API clients in one call.
    /// This is the easiest way to start using the Guardhouse users, roles, and permissions endpoints.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureClientAction">Required Guardhouse client configuration.</param>
    /// <param name="configureUserAction">Optional Guardhouse API configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithApiClients(
        this IServiceCollection services,
        Action<GuardhouseClientOptions> configureClientAction,
        Action<GuardhouseUserOptions>? configureUserAction = null)
    {
        ArgumentNullException.ThrowIfNull(configureClientAction);

        services.AddGuardhouseClient(configureClientAction);
        services.AddGuardhouseApiClients(configureUserAction);
        return services;
    }

    /// <summary>
    /// Adds Guardhouse client and users API services in one call.
    /// This method is kept as a backward-compatible alias for AddGuardhouseClientWithApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureClientAction">Required Guardhouse client configuration.</param>
    /// <param name="configureUserAction">Optional Guardhouse API configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithUserService(
        this IServiceCollection services,
        Action<GuardhouseClientOptions> configureClientAction,
        Action<GuardhouseUserOptions>? configureUserAction = null)
    {
        return services.AddGuardhouseClientWithApiClients(configureClientAction, configureUserAction);
    }

    /// <summary>
    /// Adds Guardhouse client and external API clients in one call.
    /// This method is kept as a backward-compatible alias for AddGuardhouseClientWithApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureClientAction">Required Guardhouse client configuration.</param>
    /// <param name="configureUserAction">Optional Guardhouse API configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithUserClients(
        this IServiceCollection services,
        Action<GuardhouseClientOptions> configureClientAction,
        Action<GuardhouseUserOptions>? configureUserAction = null)
    {
        return services.AddGuardhouseClientWithApiClients(configureClientAction, configureUserAction);
    }

    /// <summary>
    /// Adds Guardhouse client and external API clients with simple configuration.
    /// Disables token refresh by default to avoid requiring offline_access for this quick-start path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="authority">The authority URL of identity server.</param>
    /// <param name="clientId">The client ID assigned to your application.</param>
    /// <param name="clientSecret">The client secret for your application.</param>
    /// <param name="scope">The scope(s) to request (default: "system_api").</param>
    /// <param name="apiBaseUrl">Optional base URL for the Guardhouse API. If omitted, falls back to Authority.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithApiClients(
        this IServiceCollection services,
        string authority,
        string clientId,
        string clientSecret,
        string scope = AuthorizationConsts.Scopes.SystemApi,
        string? apiBaseUrl = null)
    {
        return services.AddGuardhouseClientWithApiClients(
            configureClientAction: options =>
            {
                options.Authority = authority;
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.Scope = scope;
                options.EnableTokenRefresh = false;
            },
            configureUserAction: options =>
            {
                if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                {
                    options.ApiBaseUrl = apiBaseUrl;
                }
            });
    }

    /// <summary>
    /// Adds Guardhouse client and users API services with simple configuration.
    /// This method is kept as a backward-compatible alias for AddGuardhouseClientWithApiClients.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="authority">The authority URL of identity server.</param>
    /// <param name="clientId">The client ID assigned to your application.</param>
    /// <param name="clientSecret">The client secret for your application.</param>
    /// <param name="scope">The scope(s) to request (default: "system_api").</param>
    /// <param name="apiBaseUrl">Optional base URL for the Guardhouse API. If omitted, falls back to Authority.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithUserService(
        this IServiceCollection services,
        string authority,
        string clientId,
        string clientSecret,
        string scope = AuthorizationConsts.Scopes.SystemApi,
        string? apiBaseUrl = null)
    {
        return services.AddGuardhouseClientWithApiClients(authority, clientId, clientSecret, scope, apiBaseUrl);
    }

    /// <summary>
    /// Adds Guardhouse client and external API clients with simple configuration.
    /// This method is kept as a backward-compatible alias for AddGuardhouseClientWithApiClients.
    /// Disables token refresh by default to avoid requiring offline_access for this quick-start path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="authority">The authority URL of identity server.</param>
    /// <param name="clientId">The client ID assigned to your application.</param>
    /// <param name="clientSecret">The client secret for your application.</param>
    /// <param name="scope">The scope(s) to request (default: "system_api").</param>
    /// <param name="apiBaseUrl">Optional base URL for the Guardhouse API. If omitted, falls back to Authority.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGuardhouseClientWithUserClients(
        this IServiceCollection services,
        string authority,
        string clientId,
        string clientSecret,
        string scope = AuthorizationConsts.Scopes.SystemApi,
        string? apiBaseUrl = null)
    {
        return services.AddGuardhouseClientWithApiClients(authority, clientId, clientSecret, scope, apiBaseUrl);
    }

    private sealed class GuardhouseClientPolicyCache(IOptions<GuardhouseClientOptions> options)
    {
        public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; } =
            BuildRetryPolicy(options.Value.MaxRetryAttempts, options.Value.EnableHttpResilience);

        public IAsyncPolicy<HttpResponseMessage> TimeoutPolicy { get; } =
            BuildTimeoutPolicy(options.Value.RequestTimeoutSeconds);
    }

    private sealed class GuardhouseResourcePolicyCache(IOptions<GuardhouseResourceOptions> options)
    {
        public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; } =
            BuildRetryPolicy(options.Value.MaxRetryAttempts);

        public IAsyncPolicy<HttpResponseMessage> TimeoutPolicy { get; } =
            BuildTimeoutPolicy(options.Value.RequestTimeoutSeconds);
    }

    private sealed class GuardhouseClientRegistrationsMarker;

    private sealed class GuardhouseResourceRegistrationsMarker;

    private sealed class GuardhouseApiClientRegistrationsMarker;

    internal static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int maxRetryAttempts, bool enabled = true)
    {
        if (!enabled || maxRetryAttempts <= 0)
        {
            return NoRetryPolicy;
        }

        // Handle network exceptions, server 5xx errors (500-599), timeouts, 429 rate limits, and 408 timeout
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .OrResult(msg => (int)msg.StatusCode is >= 500 and < 600)
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.BadGateway)
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                maxRetryAttempts,
                (retryAttempt, result, _) =>
                    GetRetryDelay(result, retryAttempt),
                (result, _, _, _) =>
                {
                    result.Result?.Dispose();
                    return Task.CompletedTask;
                });
    }

    private static bool IsOfflineAccessScopeValid(GuardhouseClientOptions options)
    {
        if (!options.EnableTokenRefresh)
        {
            return true;
        }

        if (options.IncludeOfflineAccessScope)
        {
            return true;
        }

        return HasScope(options.Scope, GuardhouseConstants.Scopes.OfflineAccess);
    }

    private static bool IsValidApiBaseUrl(string apiBaseUrl)
    {
        return Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasScope(string? scope, string scopeToFind)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(s => string.Equals(s, scopeToFind, StringComparison.OrdinalIgnoreCase));
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

        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
        return baseDelay + jitter;
    }

    private static TimeSpan? TryGetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return null;
        }

        if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
        {
            return retryAfter.Delta.Value;
        }

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return null;
    }

    private static bool IsSafeRetryMethod(HttpMethod method)
    {
        return method == HttpMethod.Get ||
               method == HttpMethod.Head ||
               method == HttpMethod.Options;
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildTimeoutPolicy(int requestTimeoutSeconds)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(requestTimeoutSeconds));
    }

    private static bool IsRegistered<TService>(IServiceCollection services)
    {
        return services.Any(descriptor => descriptor.ServiceType == typeof(TService));
    }

    private static void AddGuardhouseApiHttpClient<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddHttpClient<TService, TImplementation>((_, client) =>
            {
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            })
            .AddPolicyHandler((sp, request) =>
                IsSafeRetryMethod(request.Method)
                    ? sp.GetRequiredService<GuardhouseClientPolicyCache>().RetryPolicy
                    : NoRetryPolicy)
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseClientPolicyCache>().TimeoutPolicy);
    }
}
