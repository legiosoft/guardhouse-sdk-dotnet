namespace Guardhouse.SDK.Services;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Permissions;

public class GuardhousePermissionsClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseApiClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhousePermissionsClient
{
    public async Task<CreatePermissionResponse> CreatePermissionAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Permissions.Collection,
            request,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "create permission", cancellationToken);

        var result = await ReadJsonAsync<CreatePermissionResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize CreatePermissionResponse");
    }

    public async Task<GetPermissionsResponse> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Permissions.Collection,
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "get permissions", cancellationToken);

        var result = await ReadJsonAsync<GetPermissionsResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetPermissionsResponse");
    }

    public async Task<GetPermissionByIdResponse?> GetPermissionByIdAsync(
        int permissionId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Permissions.ById(permissionId),
            null,
            cancellationToken);

        var exists = await ReturnFalseOnNotFoundAsync(response, $"get permission '{permissionId}'", cancellationToken);
        if (!exists)
        {
            return null;
        }

        var result = await ReadJsonAsync<GetPermissionByIdResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetPermissionByIdResponse");
    }

    public async Task<bool> UpdatePermissionAsync(
        int permissionId,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Permissions.ById(permissionId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"update permission '{permissionId}'", cancellationToken);
    }
}
