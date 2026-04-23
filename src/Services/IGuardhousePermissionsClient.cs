namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Permissions;

/// <summary>
/// Client interface for Guardhouse permissions API endpoints.
/// </summary>
public interface IGuardhousePermissionsClient
{
    Task<CreatePermissionResponse> CreatePermissionAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken = default);

    Task<GetPermissionsResponse> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<GetPermissionByIdResponse?> GetPermissionByIdAsync(
        int permissionId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdatePermissionAsync(
        int permissionId,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken = default);
}
