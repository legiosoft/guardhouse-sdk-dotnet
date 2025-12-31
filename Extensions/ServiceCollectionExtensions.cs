using System;
using System.Net.Http;
using System.Threading.Tasks;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using Polly;
using Polly.Extensions;
using Polly.Retry;

namespace Guardhouse.SDK;

/// <summary>
/// Extension methods for configuring Guardhouse SDK services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Guardhouse client services to the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureAction">Configuration action for client options</param>
    public static IServiceCollection AddGuardhouseClient(
        this IServiceCollection services,
        Action<GuardhouseClientOptions>? configureAction = null)
    {
        if (configureAction != null)
        {
            services.Configure(configureAction);
        }
        else
        {
            // Register options with default validation
            services.AddOptions<GuardhouseClientOptions>()
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        // Add NodaTime clock
        services.AddSingleton<IClock>(SystemClock.Instance);

        // Add HttpClient with resilience pipeline
        services.AddHttpClient<IGuardhouseTokenService, GuardhouseTokenService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        services.AddMemoryCache();
        services.AddScoped<IGuardhouseTokenService, GuardhouseTokenService>();

        return services;
    }

    /// <summary>
    /// Adds Guardhouse resource server services to the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureAction">Configuration action for resource options</param>
    public static IServiceCollection AddGuardhouseResource(
        this IServiceCollection services,
        Action<GuardhouseResourceOptions>? configureAction = null)
    {
        if (configureAction != null)
        {
            services.Configure(configureAction);
        }
        else
        {
            // Register options with default validation
            services.AddOptions<GuardhouseResourceOptions>()
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddScoped<GuardhouseJwtBearerEvents>();
        services.AddScoped<IGuardhouseResourceService, GuardhouseResourceService>();

        // Configure JWT bearer options from GuardhouseResourceOptions
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, GuardhouseJwtBearerOptionsConfigurer>();
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, GuardhouseJwtBearerOptionsConfigurer>();

        return services;
    }

    /// <summary>
    /// Adds both Guardhouse client and resource server services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureClientAction">Configuration action for client options</param>
    /// <param name="configureResourceAction">Configuration action for resource options</param>
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
    /// Adds Guardhouse client services with minimal configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="authority">Guardhouse server URL</param>
    /// <param name="clientId">Client ID</param>
    /// <param name="clientSecret">Client secret</param>
    /// <param name="scope">Requested scope (default: "api")</param>
    public static IServiceCollection AddGuardhouseClient(
        this IServiceCollection services,
        string authority,
        string clientId,
        string clientSecret,
        string scope = "api")
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
    /// Adds Guardhouse resource server services with minimal configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="authority">Guardhouse server URL</param>
    /// <param name="audience">Expected audience (optional)</param>
    public static IServiceCollection AddGuardhouseResource(
        this IServiceCollection services,
        string authority,
        string? audience = null)
    {
        return services.AddGuardhouseResource(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
        });
    }

    /// <summary>
    /// Configures HttpClient resilience pipeline for Guardhouse services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureAction">Configuration action for resilience options</param>
    public static IServiceCollection ConfigureGuardhouseResilience(
        this IServiceCollection services,
        Action<GuardhouseResilienceOptions>? configureAction = null)
    {
        if (configureAction != null)
        {
            services.Configure(configureAction);
        }

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(30);
    }
}

/// <summary>
/// Options for configuring resilience behavior
/// </summary>
public class GuardhouseResilienceOptions
{
    /// <summary>
    /// Maximum retry attempts (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Request timeout in seconds (default: 30)
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable exponential backoff (default: true)
    /// </summary>
    public bool EnableExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Base delay for retries in seconds (default: 1)
    /// </summary>
    public double BaseRetryDelaySeconds { get; set; } = 1;
}