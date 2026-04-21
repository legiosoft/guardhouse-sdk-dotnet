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
using Models.Users.Roles;

public class GuardhouseUserRolesClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseUserManagementClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhouseUserRolesClient
{
    public async Task<GetUserRolesResponse?> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Users.Roles(userId),
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(response, $"get roles for user '{userId}'", cancellationToken);
        return await ReadJsonAsync<GetUserRolesResponse>(response, cancellationToken);
    }

    public async Task<bool> SetUserRolesAsync(int userId, SetUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Users.Roles(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"set roles for user '{userId}'", cancellationToken);
    }

    public async Task<bool> AddUserRolesAsync(int userId, AddUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Roles(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"add roles for user '{userId}'", cancellationToken);
    }

    public async Task<bool> RemoveUserRolesAsync(int userId, RemoveUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Users.Roles(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"remove roles for user '{userId}'", cancellationToken);
    }
}
