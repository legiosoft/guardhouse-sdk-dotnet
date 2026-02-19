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
    /// <param name="services">The service collection to add services to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Guardhouse client services to dependency injection container.
        /// This enables your application to obtain access tokens from identity server.
        /// </summary>
        /// <param name="configureAction">Optional action to configure Guardhouse client options.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddGuardhouseClient(Action<GuardhouseClientOptions>? configureAction = null)
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

            // AddHttpClient already registers the service - no need for AddScoped
            services.AddHttpClient<IGuardhouseTokenService, GuardhouseTokenService>((_, client) =>
                {
                    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                })
                .AddPolicyHandler((sp, _) => GetRetryPolicy(sp))
                .AddPolicyHandler((sp, _) => GetTimeoutPolicy(sp));

            return services;
        }

        /// <summary>
        /// Adds Guardhouse resource server services to dependency injection container.
        /// This enables your application to validate incoming JWT tokens.
        /// </summary>
        /// <param name="configureAction">Optional action to configure Guardhouse resource options.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddGuardhouseResource(Action<GuardhouseResourceOptions>? configureAction = null)
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

                    if (options.ValidationMode == TokenValidationMode.Introspection)
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

            // Add the Guardhouse scheme without overriding the default
            services.AddAuthentication()
                .AddJwtBearer(GuardhouseConstants.Authentication.DefaultScheme, _ => { });

            // AddHttpClient already registers the service - no need for AddScoped
            services.AddHttpClient<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>((_, client) =>
                {
                    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
                })
                .AddPolicyHandler((sp, _) => GetRetryPolicyForResource(sp))
                .AddPolicyHandler((sp, _) => GetTimeoutPolicyForResource(sp));

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
        /// <param name="configureClientAction">Optional action to configure Guardhouse client options.</param>
        /// <param name="configureResourceAction">Optional action to configure Guardhouse resource options.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddGuardhouse(
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
        /// <param name="authority">The authority URL of identity server.</param>
        /// <param name="clientId">The client ID assigned to your application.</param>
        /// <param name="clientSecret">The client secret for your application.</param>
        /// <param name="scope">The scope(s) to request (default: "api").</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddGuardhouseClient(
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
        /// <param name="authority">The authority URL of identity server.</param>
        /// <param name="audience">The audience that your resource server expects.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddGuardhouseResource(
            string authority,
            string audience)
        {
            return services.AddGuardhouseResource(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
            });
        }
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;

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
                options.MaxRetryAttempts,
                retryAttempt =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                    return baseDelay + jitter;
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicyForResource(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

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
                options.MaxRetryAttempts,
                retryAttempt =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                    return baseDelay + jitter;
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicyForResource(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
    }
}
