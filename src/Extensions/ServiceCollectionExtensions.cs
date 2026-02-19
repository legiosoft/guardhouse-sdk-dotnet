// ReSharper disable RedundantNameQualifier

namespace Guardhouse.SDK.Extensions;

using System.Net.Http;
using Constants;
using Models;
using Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;
using Polly.Timeout;

/// <summary>
/// Extension methods for configuring Guardhouse services in dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
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

        services.AddOptions<GuardhouseClientOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddMemoryCache();
        services.AddSingleton<GuardhouseClientPolicyCache>();

        // AddHttpClient already registers the service - no need for AddScoped
        services.AddHttpClient<IGuardhouseTokenService, GuardhouseTokenService>((_, client) =>
            {
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            })
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseClientPolicyCache>().RetryPolicy)
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<GuardhouseClientPolicyCache>().TimeoutPolicy);

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
        services.AddSingleton<GuardhouseResourcePolicyCache>();

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
        services.AddScoped<GuardhouseIntrospectionJwtBearerEvents>();
        services.AddScoped<IGuardhouseResourceService, GuardhouseResourceService>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureGuardhouseJwtOptions>();
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureGuardhouseJwtOptions>();

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

    private sealed class GuardhouseClientPolicyCache(IOptions<GuardhouseClientOptions> options)
    {
        public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; } =
            BuildRetryPolicy(options.Value.MaxRetryAttempts);

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

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int maxRetryAttempts)
    {
        if (maxRetryAttempts <= 0)
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
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
                (int retryAttempt, DelegateResult<HttpResponseMessage> result, Context _) =>
                    GetRetryDelay(result, retryAttempt),
                (_, _, _, _) => Task.CompletedTask);
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

    private static IAsyncPolicy<HttpResponseMessage> BuildTimeoutPolicy(int requestTimeoutSeconds)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(requestTimeoutSeconds));
    }
}