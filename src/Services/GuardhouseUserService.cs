namespace Guardhouse.SDK.Services;

using System;
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
using Models.Users;

public class GuardhouseUserService(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger<GuardhouseUserService>? logger = null) : IGuardhouseUserService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<GuardhouseUserService> _logger = logger ?? NullLogger<GuardhouseUserService>.Instance;
    private readonly GuardhouseUserOptions _userOptions = userOptions.Value;
    private readonly Uri _apiBaseUri = ResolveApiBaseUri(userOptions.Value, clientOptions.Value);

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            "api/v1/users",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictDetails = await ReadResponseContentAsync(response, cancellationToken);
            throw new InvalidOperationException($"Failed to create user. {conflictDetails}");
        }

        await EnsureSuccessStatusCodeAsync(response, "create user", cancellationToken);

        var result = await ReadJsonAsync<CreateUserResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize CreateUserResponse");
    }

    public async Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            $"api/v1/users/{userId}",
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(response, $"get user by id '{userId}'", cancellationToken);

        var result = await ReadJsonAsync<GetUserByIdResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetUserByIdResponse");
    }

    public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            $"api/v1/users/{userId}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessStatusCodeAsync(response, $"update user '{userId}'", cancellationToken);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            $"api/v1/users/{userId}/password",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var badRequestDetails = await ReadResponseContentAsync(response, cancellationToken);
            throw new InvalidOperationException($"Failed to change user password. {badRequestDetails}");
        }

        await EnsureSuccessStatusCodeAsync(response, $"change password for user '{userId}'", cancellationToken);
        return true;
    }

    private async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(
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

    private static Uri ResolveApiBaseUri(GuardhouseUserOptions userOptions, GuardhouseClientOptions clientOptions)
    {
        var candidateBaseUrl = !string.IsNullOrWhiteSpace(userOptions.ApiBaseUrl)
            ? userOptions.ApiBaseUrl
            : clientOptions.Authority;

        if (string.IsNullOrWhiteSpace(candidateBaseUrl))
        {
            throw new InvalidOperationException(
                "Guardhouse user API base URL is not configured. " +
                "Set GuardhouseUserOptions.ApiBaseUrl or configure GuardhouseClientOptions.Authority.");
        }

        if (!Uri.TryCreate(candidateBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Guardhouse user API base URL must be an absolute URI.");
        }

        if (clientOptions.RequireHttps &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTPS is required for Guardhouse user API endpoint.");
        }

        return uri;
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseDetails = await ReadResponseContentAsync(response, cancellationToken);
        throw new InvalidOperationException(
            $"Failed to {operation}. " +
            $"Status: {response.StatusCode} ({(int)response.StatusCode}). " +
            responseDetails);
    }

    private static async Task<string> ReadResponseContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Response body is empty.";
        }

        return $"Response: {content}";
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(content, SerializerOptions);
    }
}
