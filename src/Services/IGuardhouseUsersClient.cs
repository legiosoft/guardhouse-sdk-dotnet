namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users;
using Models.Users.Privacy;

/// <summary>
/// Client interface for Guardhouse users API endpoints.
/// </summary>
public interface IGuardhouseUsersClient
{
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<GetUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default);

    Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<bool> AssignUserToRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);

    Task<bool> UnassignUserFromRoleAsync(
        int userId,
        int roleId,
        UnassignUserFromRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> BlockUserAsync(int userId, BlockUserRequest request, CancellationToken cancellationToken = default);

    Task<bool> UnblockUserAsync(int userId, UnblockUserRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteUserPersonalDataAsync(
        int userId,
        DeleteUserPersonalDataRequest request,
        CancellationToken cancellationToken = default);
}
