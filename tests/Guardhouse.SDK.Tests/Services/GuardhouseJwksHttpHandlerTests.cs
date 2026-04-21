using System.Net;
using System.Net.Http;
using FluentAssertions;
using Guardhouse.SDK.Services;
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestUris.Add(requestUri);

            if (!responses.TryGetValue(requestUri, out var response))
            {
                throw new InvalidOperationException($"Unexpected request URI: {requestUri}");
            }

            return Task.FromResult(response);
        }
    }
}
