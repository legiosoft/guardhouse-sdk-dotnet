namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users;

/// <summary>
/// Client interface for user lifecycle operations in Guardhouse machine-to-machine APIs.
/// </summary>
public interface IGuardhouseUsersClient
{
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
