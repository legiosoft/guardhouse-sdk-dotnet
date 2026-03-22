using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

using Microsoft.Extensions.Caching.Memory;
using SDK.Services;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGuardhouseClient_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.Scope = "api";
            options.IncludeOfflineAccessScope = true;
        });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IMemoryCache));
    }

    [Fact]
    public void AddGuardhouseClient_WithMinimalParameters_ShouldConfigureOptions()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(
            authority: "https://test.com",
            clientId: "test-client",
            clientSecret: "test-secret",
            scope: "api offline_access");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;

        options.Authority.Should().Be("https://test.com");
        options.ClientId.Should().Be("test-client");
        options.ClientSecret.Should().Be("test-secret");
        options.Scope.Should().Be("api offline_access");
    }

    [Fact]
    public void AddGuardhouseClient_WithRefreshEnabledWithoutOfflineAccess_ShouldFailValidation()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.Scope = "api";
            options.EnableTokenRefresh = true;
            options.IncludeOfflineAccessScope = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        Action act = () => _ = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*offline_access*");
    }

    [Fact]
    public void AddGuardhouseResource_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
        });

        services.Should().Contain(sd => sd.ServiceType == typeof(IAuthenticationService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseResourceService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseIntrospectionService));
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionMode_ShouldConfigureCorrectly()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "introspection-client";
            options.IntrospectionClientSecret = "introspection-secret";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.EnableIntrospection.Should().BeTrue();
        options.IntrospectionClientId.Should().Be("introspection-client");
        options.IntrospectionClientSecret.Should().Be("introspection-secret");
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionMode_ShouldRequireIntrospectionCredentials()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "introspection-client";
            options.IntrospectionClientSecret = "introspection-secret";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.IntrospectionClientId.Should().Be("introspection-client");
        options.IntrospectionClientSecret.Should().Be("introspection-secret");
    }

    [Fact]
    public void AddGuardhouseResource_WithIntrospectionClientIdDifferentFromAudience_ShouldLogWarning()
    {
        var services = new ServiceCollection();
        var logSink = new TestLogSink();
        services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider(logSink)));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "resource-api";
            options.ValidationMode = TokenValidationMode.Introspection;
            options.IntrospectionClientId = "introspection-client";
            options.IntrospectionClientSecret = "introspection-secret";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var jwtOptionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        _ = jwtOptionsMonitor.Get(GuardhouseConstants.Authentication.DefaultScheme);

        logSink.Entries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Warning &&
            entry.Message.Contains("IntrospectionClientId", StringComparison.Ordinal) &&
            entry.Message.Contains("Audience", StringComparison.Ordinal) &&
            entry.Message.Contains("resource-api", StringComparison.Ordinal) &&
            entry.Message.Contains("introspection-client", StringComparison.Ordinal) &&
            entry.Message.Contains("may not work", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddGuardhouseResource_WithJwtSignatureMode_ShouldNotRequireIntrospectionCredentials()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddGuardhouseResource(options =>
        {
            options.Authority = "https://test.com";
            options.Audience = "test-audience";
            options.ValidationMode = TokenValidationMode.JwtSignature;
        });

        act.Should().NotThrow();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseResourceOptions>>().Value;

        options.EnableIntrospection.Should().BeFalse();
        options.IntrospectionClientId.Should().BeNull();
        options.IntrospectionClientSecret.Should().BeNull();
    }

    [Fact]
    public void AddGuardhouse_WithBothConfigurations_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouse(
            configureClientAction: co => {
                co.Authority = "https://test.com";
                co.ClientId = "test-client";
                co.ClientSecret = "test-secret";
                co.IncludeOfflineAccessScope = true;
            },
            configureResourceAction: ro => {
                ro.Authority = "https://test.com";
                ro.Audience = "test-audience";
            });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseResourceService));
    }

    [Fact]
    public void AddGuardhouseUserService_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.IncludeOfflineAccessScope = true;
        });

        services.AddGuardhouseUserService(options =>
        {
            options.ApiBaseUrl = "https://api.test.com";
        });

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseUserService));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<GuardhouseUserOptions>>().Value;
        options.ApiBaseUrl.Should().Be("https://api.test.com");
    }

    [Fact]
    public void AddGuardhouseUserService_WithRelativeApiBaseUrl_ShouldFailValidation()
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.IncludeOfflineAccessScope = true;
        });

        services.AddGuardhouseUserService(options =>
        {
            options.ApiBaseUrl = "/api";
        });

        var serviceProvider = services.BuildServiceProvider();

        Action act = () => _ = serviceProvider.GetRequiredService<IOptions<GuardhouseUserOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ApiBaseUrl must be an absolute URI*");
    }

    [Fact]
    public void AddGuardhouseClientWithUserService_ShouldRegisterClientAndUserServices()
    {
        var services = new ServiceCollection();

        services.AddGuardhouseClientWithUserService(
            authority: "https://test.com",
            clientId: "test-client",
            clientSecret: "test-secret",
            scope: "api",
            apiBaseUrl: "https://api.test.com");

        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseTokenService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IGuardhouseUserService));

        using var serviceProvider = services.BuildServiceProvider();
        var clientOptions = serviceProvider.GetRequiredService<IOptions<GuardhouseClientOptions>>().Value;
        clientOptions.EnableTokenRefresh.Should().BeFalse();
    }

    [Fact]
    public async Task AddGuardhouseUserService_ShouldReuseClientRetryPolicy()
    {
        var requestCount = 0;
        var services = new ServiceCollection();

        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.IncludeOfflineAccessScope = true;
            options.MaxRetryAttempts = 1;
        });

        services.AddGuardhouseUserService(options =>
        {
            options.ApiBaseUrl = "https://api.test.com";
        });

        services.AddSingleton<IGuardhouseTokenService>(new StaticTokenService("test-access-token"));
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = new SequenceHttpMessageHandler(() =>
                {
                    requestCount++;
                    if (requestCount == 1)
                    {
                        var throttledResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        throttledResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                        return throttledResponse;
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var userService = serviceProvider.GetRequiredService<IGuardhouseUserService>();

        var user = await userService.GetUserByIdAsync(1);

        user.Should().BeNull();
        requestCount.Should().Be(2, "one retry should be applied for 429 responses");
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class StaticTokenService(string token) : IGuardhouseTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(token);
        }

        public Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IntrospectionResponse> IntrospectTokenAsync(string tokenValue, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsTokenActiveAsync(string tokenValue, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SequenceHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory());
        }
    }

    private sealed class TestLogSink
    {
        public ConcurrentBag<LogEntry> Entries { get; } = new();
    }

    private sealed class TestLoggerProvider(TestLogSink logSink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(logSink);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(TestLogSink logSink) : ILogger
    {
        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logSink.Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
