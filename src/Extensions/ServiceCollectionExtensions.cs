using System;
using System.Net.Http;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Polly;

namespace Guardhouse.SDK.Extensions;

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

        services.AddMemoryCache();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddHttpClient<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(GuardhouseConstants.Defaults.RequestTimeoutSeconds);
        });
        services.AddScoped<IGuardhouseIntrospectionService, GuardhouseIntrospectionService>();
        services.AddScoped<IGuardhouseResourceService, GuardhouseResourceService>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, GuardhouseJwtBearerOptionsConfigurer>();
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, GuardhouseJwtBearerOptionsConfigurer>();

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
        string? audience = null)
    {
        return services.AddGuardhouseResource(options =>
        {
            options.Authority = authority;
            options.Audience = audience ?? "my_resource_api";
        });
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
