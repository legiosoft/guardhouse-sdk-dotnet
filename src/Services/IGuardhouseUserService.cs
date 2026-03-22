namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users;

/// <summary>
/// Service interface for managing users via Guardhouse machine-to-machine API endpoints.
/// </summary>
public interface IGuardhouseUserService
{
    /// <summary>
    /// Creates a new user in Guardhouse.
    /// </summary>
    /// <param name="request">User creation payload.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Response containing newly created user identifier.</returns>
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user details by numeric identifier.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>User details when found, otherwise <c>null</c>.</returns>
    Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user by identifier.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="request">User update payload.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><c>true</c> when updated, <c>false</c> when user is not found.</returns>
    Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's password by identifier.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="request">Password change payload.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><c>true</c> when changed, <c>false</c> when user is not found.</returns>
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
