namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Roles;

/// <summary>
/// Client interface for Guardhouse roles API endpoints.
/// </summary>
public interface IGuardhouseRolesClient
{
    Task<CreateRoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<GetRolesResponse> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<GetRoleByIdResponse?> GetRoleByIdAsync(int roleId, CancellationToken cancellationToken = default);

    Task<bool> UpdateRoleAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task<bool> AddPermissionsToRoleAsync(
        int roleId,
        AddPermissionsToRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RemovePermissionsFromRoleAsync(
        int roleId,
        RemovePermissionsFromRoleRequest request,
        CancellationToken cancellationToken = default);
}
