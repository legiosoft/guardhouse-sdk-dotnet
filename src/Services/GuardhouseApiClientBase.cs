namespace Guardhouse.SDK.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;

public abstract class GuardhouseApiClientBase(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
{
    protected static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly GuardhouseUserOptions _userOptions = userOptions.Value;
    private readonly Uri _apiBaseUri = ResolveApiBaseUri(userOptions.Value, clientOptions.Value);

    protected async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, new Uri(_apiBaseUri, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, SerializerOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        _logger.LogDebugIf(_userOptions.EnableDebug, "Sending {Method} request to {Path}", method, relativePath);

        return await httpClient.SendAsync(request, cancellationToken);
    }

    protected static string AppendQueryString(string relativePath, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var segments = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}")
            .ToArray();

        return segments.Length == 0
            ? relativePath
            : $"{relativePath}?{string.Join("&", segments)}";
    }

    protected static async Task EnsureSuccessStatusCodeAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseDetails = await ReadSafeResponseSummaryAsync(response, cancellationToken);
        throw new InvalidOperationException(
            $"Failed to {operation}. " +
            $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
            responseDetails);
    }

    protected static async Task<string> ReadSafeResponseSummaryAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await GuardhouseErrorResponseSanitizer.SummarizeAsync(response.Content, cancellationToken);
    }

    protected static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(content, SerializerOptions);
    }

    protected static async Task<bool> ReturnFalseOnNotFoundAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessStatusCodeAsync(response, operation, cancellationToken);
        return true;
    }

    private static Uri ResolveApiBaseUri(GuardhouseUserOptions userOptions, GuardhouseClientOptions clientOptions)
    {
        var candidateBaseUrl = !string.IsNullOrWhiteSpace(userOptions.ApiBaseUrl)
            ? userOptions.ApiBaseUrl
            : clientOptions.Authority;

        if (string.IsNullOrWhiteSpace(candidateBaseUrl))
        {
            throw new InvalidOperationException(
                "Guardhouse API base URL is not configured. " +
                "Set GuardhouseUserOptions.ApiBaseUrl or configure GuardhouseClientOptions.Authority.");
        }

        if (!Uri.TryCreate(candidateBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Guardhouse API base URL must be an absolute URI.");
        }

        if (clientOptions.RequireHttps &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for Guardhouse API endpoint.");
        }

        return uri;
    }
}
