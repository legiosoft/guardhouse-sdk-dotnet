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

public static class ServiceCollectionExtensions
{
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

    public static IServiceCollection AddGuardhouse(
        this IServiceCollection services,
        Action<GuardhouseClientOptions>? configureClientAction = null,
        Action<GuardhouseResourceOptions>? configureResourceAction = null)
    {
        services.AddGuardhouseClient(configureClientAction);
        services.AddGuardhouseResource(configureResourceAction);
        return services;
    }

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
