namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users.Roles;

/// <summary>
/// Client interface for Guardhouse user role management endpoints.
/// </summary>
public interface IGuardhouseUserRolesClient
{
    Task<GetUserRolesResponse?> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> SetUserRolesAsync(int userId, SetUserRolesRequest request, CancellationToken cancellationToken = default);

    Task<bool> AddUserRolesAsync(int userId, AddUserRolesRequest request, CancellationToken cancellationToken = default);

    Task<bool> RemoveUserRolesAsync(int userId, RemoveUserRolesRequest request, CancellationToken cancellationToken = default);
}
