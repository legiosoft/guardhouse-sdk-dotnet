namespace Guardhouse.SDK.Services;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Roles;

public class GuardhouseRolesClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseApiClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhouseRolesClient
{
    public async Task<CreateRoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Roles.Collection,
            request,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "create role", cancellationToken);

        var result = await ReadJsonAsync<CreateRoleResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize CreateRoleResponse");
    }

    public async Task<GetRolesResponse> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Roles.Collection,
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "get roles", cancellationToken);

        var result = await ReadJsonAsync<GetRolesResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetRolesResponse");
    }

    public async Task<GetRoleByIdResponse?> GetRoleByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Roles.ById(roleId),
            null,
            cancellationToken);

        var exists = await ReturnFalseOnNotFoundAsync(response, $"get role '{roleId}'", cancellationToken);
        if (!exists)
        {
            return null;
        }

        var result = await ReadJsonAsync<GetRoleByIdResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetRoleByIdResponse");
    }

    public async Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Roles.ById(roleId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"update role '{roleId}'", cancellationToken);
    }

    public async Task<bool> AddPermissionsToRoleAsync(
        int roleId,
        AddPermissionsToRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Roles.Permissions(roleId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(
            response,
            $"add permissions to role '{roleId}'",
            cancellationToken);
    }

    public async Task<bool> RemovePermissionsFromRoleAsync(
        int roleId,
        RemovePermissionsFromRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Roles.Permissions(roleId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(
            response,
            $"remove permissions from role '{roleId}'",
            cancellationToken);
    }
}
