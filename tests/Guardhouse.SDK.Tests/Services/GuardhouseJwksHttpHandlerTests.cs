using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using FluentAssertions;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseJwksHttpHandlerTests
{
    [Fact]
    public void Constructor_WithHttpClientHandler_DisablesAutoRedirect()
    {
        var innerHandler = new HttpClientHandler
        {
            AllowAutoRedirect = true
        };

        _ = CreateHandler(innerHandler, ["auth.example.com"]);

        innerHandler.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithSocketsHttpHandler_DisablesAutoRedirect()
    {
        using var innerHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true
        };

        _ = CreateHandler(innerHandler, ["auth.example.com"]);

        innerHandler.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithRedirectToDisallowedHost_ShouldThrow()
    {
        var responses = new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://auth.example.com/.well-known/jwks"] = CreateRedirectResponse("https://evil.example.com/.well-known/jwks")
        };

        using var client = new HttpClient(CreateHandler(new RecordingHttpMessageHandler(responses), ["auth.example.com"]));

        var act = async () => await client.GetAsync("https://auth.example.com/.well-known/jwks");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Backchannel host is not allowed: evil.example.com*");
    }

    [Fact]
    public async Task SendAsync_WithRedirectToHttpEndpoint_WhenHttpsRequired_ShouldThrow()
    {
        var responses = new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://auth.example.com/.well-known/jwks"] = CreateRedirectResponse("http://auth.example.com/.well-known/jwks")
        };

        using var client = new HttpClient(CreateHandler(new RecordingHttpMessageHandler(responses), ["auth.example.com"], requireHttps: true));

        var act = async () => await client.GetAsync("https://auth.example.com/.well-known/jwks");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*HTTPS is required for metadata and JWKS endpoints*");
    }

    [Fact]
    public async Task SendAsync_WithRedirectToAllowlistedHost_ShouldFollowRedirect()
    {
        var recordingHandler = new RecordingHttpMessageHandler(new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://auth.example.com/.well-known/openid-configuration"] =
                CreateRedirectResponse("https://cdn.example.com/metadata/jwks.json"),
            ["https://cdn.example.com/metadata/jwks.json"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"keys\":[{\"kid\":\"key-1\"}]}")
                }
        });

        using var client = new HttpClient(CreateHandler(recordingHandler, ["auth.example.com", "cdn.example.com"]));

        using var response = await client.GetAsync("https://auth.example.com/.well-known/openid-configuration");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"kid\":\"key-1\"");
        recordingHandler.RequestUris.Should().ContainInOrder(
            "https://auth.example.com/.well-known/openid-configuration",
            "https://cdn.example.com/metadata/jwks.json");
    }

    [Fact]
    public async Task SendAsync_WithRedirectToDifferentAllowedHost_ShouldNotForwardSensitiveHeaders()
    {
        var recordingHandler = new RecordingHttpMessageHandler(new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://auth.example.com/.well-known/jwks"] =
                CreateRedirectResponse("https://cdn.example.com/.well-known/jwks"),
            ["https://cdn.example.com/.well-known/jwks"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"keys\":[{\"kid\":\"key-1\"}]}")
                }
        });

        using var client = new HttpClient(CreateHandler(recordingHandler, ["auth.example.com", "cdn.example.com"]));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://auth.example.com/.well-known/jwks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        request.Headers.Add("Cookie", "session=abc123");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        recordingHandler.Requests.Should().HaveCount(2);
        recordingHandler.Requests[0].Authorization.Should().Be("Bearer secret-token");
        recordingHandler.Requests[0].Cookie.Should().Be("session=abc123");
        recordingHandler.Requests[1].Authorization.Should().BeNull();
        recordingHandler.Requests[1].Cookie.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WithRedirectToSameHost_ShouldPreserveAuthorizationHeader()
    {
        var recordingHandler = new RecordingHttpMessageHandler(new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://auth.example.com/.well-known/jwks"] =
                CreateRedirectResponse("https://auth.example.com/metadata/jwks"),
            ["https://auth.example.com/metadata/jwks"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"keys\":[{\"kid\":\"key-1\"}]}")
                }
        });

        using var client = new HttpClient(CreateHandler(recordingHandler, ["auth.example.com"]));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://auth.example.com/.well-known/jwks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "same-host-token");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        recordingHandler.Requests.Should().HaveCount(2);
        recordingHandler.Requests[1].Authorization.Should().Be("Bearer same-host-token");
    }

    [Fact]
    public async Task SendAsync_WithOversizedWellKnownResponse_ShouldSkipInspection()
    {
        var logger = new RecordingLogger<GuardhouseJwksHttpHandler>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"keys\":[{\"kid\":\"key-1\"}]}")
        };
        response.Content.Headers.ContentLength = 1024 * 1024 + 1;

        using var client = new HttpClient(new GuardhouseJwksHttpHandler(
            new RecordingHttpMessageHandler(new Dictionary<string, HttpResponseMessage>(StringComparer.OrdinalIgnoreCase)
            {
                ["https://auth.example.com/.well-known/jwks"] = response
            }),
            logger,
            maxRetryAttempts: 0,
            ["auth.example.com"],
            requireHttps: true));

        using var httpResponse = await client.GetAsync("https://auth.example.com/.well-known/jwks");

        httpResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        logger.Messages.Should().Contain(message =>
            message.Level == LogLevel.Warning &&
            message.Message.Contains("exceeds the inspection limit", StringComparison.Ordinal));
    }

    [Fact]
    public void TryGetRetryAfterDelay_WithExcessiveDelay_ShouldCapDelay()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));

        var method = typeof(GuardhouseJwksHttpHandler).GetMethod(
            "TryGetRetryAfterDelay",
            BindingFlags.NonPublic | BindingFlags.Static);

        var delay = (TimeSpan?)method!.Invoke(null, [response]);

        delay.Should().Be(TimeSpan.FromSeconds(30));
    }

    private static GuardhouseJwksHttpHandler CreateHandler(
        HttpMessageHandler innerHandler,
        IReadOnlyCollection<string> allowedHosts,
        bool requireHttps = true)
    {
        return new GuardhouseJwksHttpHandler(
            innerHandler,
            NullLogger<GuardhouseJwksHttpHandler>.Instance,
            maxRetryAttempts: 0,
            allowedHosts,
            requireHttps);
    }

    private static HttpResponseMessage CreateRedirectResponse(string location)
    {
        return new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers =
            {
                Location = new Uri(location, UriKind.Absolute)
            }
        };
    }

    private sealed class RecordingHttpMessageHandler(IDictionary<string, HttpResponseMessage> responses) : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = [];
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestUris.Add(requestUri);
            Requests.Add(new CapturedRequest(
                requestUri,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Cookie", out var cookieValues)
                    ? string.Join("; ", cookieValues)
                    : null));

            if (!responses.TryGetValue(requestUri, out var response))
            {
                throw new InvalidOperationException($"Unexpected request URI: {requestUri}");
            }

            return Task.FromResult(response);
        }
    }

    private sealed record CapturedRequest(string Uri, string? Authorization, string? Cookie);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<RecordedMessage> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
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
            Messages.Add(new RecordedMessage(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record RecordedMessage(LogLevel Level, string Message);
}
