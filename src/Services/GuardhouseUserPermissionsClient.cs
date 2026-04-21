namespace Guardhouse.SDK.Services;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Users.Permissions;

public class GuardhouseUserPermissionsClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseUserManagementClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhouseUserPermissionsClient
{
    public async Task<GetUserPermissionsResponse?> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Users.Permissions(userId),
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(response, $"get permissions for user '{userId}'", cancellationToken);
        return await ReadJsonAsync<GetUserPermissionsResponse>(response, cancellationToken);
    }

    public async Task<bool> SetUserPermissionsAsync(int userId, SetUserPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Users.Permissions(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"set permissions for user '{userId}'", cancellationToken);
    }

    public async Task<bool> AddUserPermissionsAsync(int userId, AddUserPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Permissions(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"add permissions for user '{userId}'", cancellationToken);
    }

    public async Task<bool> RemoveUserPermissionsAsync(int userId, RemoveUserPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Users.Permissions(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"remove permissions for user '{userId}'", cancellationToken);
    }
}
