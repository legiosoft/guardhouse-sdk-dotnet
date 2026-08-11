using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Models.Users;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

public class HttpResilienceTests
{
    [Fact]
    public async Task TokenClient_WithDefaultRetryCount_ShouldSendAtMostFourRequests()
    {
        var requestCount = 0;
        using var serviceProvider = CreateTokenServiceProvider(_ =>
        {
            Interlocked.Increment(ref requestCount);
            return CreateThrottledResponse();
        });

        var tokenService = serviceProvider.GetRequiredService<IGuardhouseTokenService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.RequestTokenAsync());

        requestCount.Should().Be(4, "three retries must mean four total requests");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task TokenClient_WithPermanentFailure_ShouldNotRetry(HttpStatusCode statusCode)
    {
        var requestCount = 0;
        using var serviceProvider = CreateTokenServiceProvider(_ =>
        {
            Interlocked.Increment(ref requestCount);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("""{"error":"permanent_failure"}""")
            };
        });

        var tokenService = serviceProvider.GetRequiredService<IGuardhouseTokenService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.RequestTokenAsync());

        requestCount.Should().Be(1);
    }

    [Fact]
    public async Task TokenClient_WithResilienceDisabled_ShouldNotRetryTransientFailure()
    {
        var requestCount = 0;
        using var serviceProvider = CreateTokenServiceProvider(
            _ =>
            {
                Interlocked.Increment(ref requestCount);
                return CreateThrottledResponse();
            },
            options => options.EnableHttpResilience = false);

        var tokenService = serviceProvider.GetRequiredService<IGuardhouseTokenService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenService.RequestTokenAsync());

        requestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task SystemApiClient_WithMutationRequest_ShouldNotRetry(string method)
    {
        var requestCount = 0;
        HttpMethod? observedMethod = null;
        using var serviceProvider = CreateApiServiceProvider(request =>
        {
            Interlocked.Increment(ref requestCount);
            observedMethod = request.Method;
            return CreateThrottledResponse();
        });

        var client = serviceProvider.GetRequiredService<IGuardhouseUsersClient>();
        Func<Task> operation = method switch
        {
            "POST" => async () => { _ = await client.AssignUserToRoleAsync(1, 2); },
            "PUT" => async () => { _ = await client.UpdateUserAsync(1, new UpdateUserRequest()); },
            "PATCH" => async () => { _ = await client.BlockUserAsync(1, new BlockUserRequest()); },
            "DELETE" => async () =>
            {
                _ = await client.UnassignUserFromRoleAsync(1, 2, new UnassignUserFromRoleRequest());
            },
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(operation);

        requestCount.Should().Be(1);
        observedMethod?.Method.Should().Be(method);
    }

    private static ServiceProvider CreateTokenServiceProvider(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        Action<GuardhouseClientOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.Scope = GuardhouseConstants.Defaults.DefaultScope;
            options.EnableTokenRefresh = false;
            options.MaxRetryAttempts = 3;
            configure?.Invoke(options);
        });
        ConfigurePrimaryHandler(services, responseFactory);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateApiServiceProvider(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var services = new ServiceCollection();
        services.AddGuardhouseClient(options =>
        {
            options.Authority = "https://test.com";
            options.ClientId = "test-client";
            options.ClientSecret = "test-secret";
            options.Scope = AuthorizationConsts.Scopes.SystemApi;
            options.EnableTokenRefresh = false;
            options.MaxRetryAttempts = 3;
        });
        services.AddGuardhouseApiClients(options =>
        {
            options.ApiBaseUrl = "https://api.test.com";
        });
        services.AddSingleton<IGuardhouseTokenService>(new StaticTokenService("test-access-token"));
        ConfigurePrimaryHandler(services, responseFactory);
        return services.BuildServiceProvider();
    }

    private static void ConfigurePrimaryHandler(
        IServiceCollection services,
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = new DelegateHttpMessageHandler(responseFactory);
            });
        });
    }

    private static HttpResponseMessage CreateThrottledResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"temporarily_unavailable"}""")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        return response;
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class StaticTokenService(string token) : IGuardhouseTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(token);

        public Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TokenResponse> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntrospectionResponse> IntrospectTokenAsync(
            string tokenValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsTokenActiveAsync(
            string tokenValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
