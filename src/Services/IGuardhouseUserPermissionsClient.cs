namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users.Permissions;

/// <summary>
/// Client interface for Guardhouse user permission management endpoints.
/// </summary>
public interface IGuardhouseUserPermissionsClient
{
    Task<GetUserPermissionsResponse?> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> SetUserPermissionsAsync(int userId, SetUserPermissionsRequest request, CancellationToken cancellationToken = default);

    Task<bool> AddUserPermissionsAsync(int userId, AddUserPermissionsRequest request, CancellationToken cancellationToken = default);

    Task<bool> RemoveUserPermissionsAsync(int userId, RemoveUserPermissionsRequest request, CancellationToken cancellationToken = default);
}
