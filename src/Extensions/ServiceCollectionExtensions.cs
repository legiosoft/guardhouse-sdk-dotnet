namespace Guardhouse.SDK.Extensions;

using System;
using System.Net.Http;
using Constants;
using Models;
using Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;

/// <summary>
/// Extension methods for configuring Guardhouse services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Guardhouse client services to the dependency injection container.
    /// This enables your application to obtain access tokens from the identity server.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse client options.</param>
    /// <returns>The service collection for method chaining.</returns>
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
            services.AddOptions<GuardhouseClientOptions>()
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        services.AddSingleton<IClock>(SystemClock.Instance);

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
    /// Adds the Guardhouse resource server services to the dependency injection container.
    /// This enables your application to validate incoming JWT tokens.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureAction">Optional action to configure Guardhouse resource options.</param>
    /// <returns>The service collection for method chaining.</returns>
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
            services.AddOptions<GuardhouseResourceOptions>()
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        services.AddOptions<GuardhouseResourceOptions>()
            .Validate(ValidateGuardhouseResourceOptions)
            .ValidateOnStart();

        services.AddMemoryCache();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddHttpClient<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>((_, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(GuardhouseConstants.Defaults.RequestTimeoutSeconds);
        });
        services.AddScoped<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>();
        services.AddScoped<IGuardhouseResourceService, GuardhouseResourceService>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureGuardhouseJwtOptions>();
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureGuardhouseJwtOptions>();

        return services;
    }

    /// <summary>
    /// Adds both Guardhouse client and resource server services to the dependency injection container.
    /// This is a convenience method that calls both AddGuardhouseClient and AddGuardhouseResource.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
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
    /// Adds the Guardhouse client services with simple configuration using individual parameters.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="authority">The authority URL of the identity server.</param>
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
    /// Adds the Guardhouse resource server services with simple configuration using individual parameters.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="authority">The authority URL of the identity server.</param>
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

    private static bool ValidateGuardhouseResourceOptions(GuardhouseResourceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new OptionsValidationException("Guardhouse Resource: Authority is required.", typeof(GuardhouseResourceOptions), ["Authority"]);
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new OptionsValidationException("Guardhouse Resource: Audience is required.", typeof(GuardhouseResourceOptions), ["Audience"]);
        }

        if (options.ValidationMode == TokenValidationMode.Introspection)
        {
            if (string.IsNullOrWhiteSpace(options.IntrospectionClientId))
            {
                throw new OptionsValidationException(
                    "Guardhouse Resource: IntrospectionClientId is required when ValidationMode is set to Introspection. " +
                    "Please configure IntrospectionClientId and IntrospectionClientSecret for introspection-based token validation.",
                    typeof(GuardhouseResourceOptions),
                    ["IntrospectionClientId"]);
            }

            if (string.IsNullOrWhiteSpace(options.IntrospectionClientSecret))
            {
                throw new OptionsValidationException(
                    "Guardhouse Resource: IntrospectionClientSecret is required when ValidationMode is set to Introspection. " +
                    "Please configure IntrospectionClientId and IntrospectionClientSecret for introspection-based token validation.",
                    typeof(GuardhouseResourceOptions),
                    ["IntrospectionClientSecret"]);
            }
        }

        return true;
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
        return Policy.TimeoutAsync<HttpResponseMessage>(GuardhouseConstants.Defaults.RequestTimeoutSeconds);
    }
}
