namespace Guardhouse.SDK.Services;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Users;
using Models.Users.Privacy;

public class GuardhouseUsersClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseApiClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhouseUsersClient
{
    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Collection,
            request,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "create user", cancellationToken);

        var result = await ReadJsonAsync<CreateUserResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize CreateUserResponse");
    }

    public async Task<GetUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = AppendQueryString(
            GuardhouseApiRoutes.Users.Collection,
            [
                new KeyValuePair<string, string?>("pageSize", request.PageSize.ToString()),
                new KeyValuePair<string, string?>("offset", request.Offset.ToString()),
                new KeyValuePair<string, string?>("email", request.Email)
            ]);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            path,
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, "get users", cancellationToken);

        var result = await ReadJsonAsync<GetUsersResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetUsersResponse");
    }

    public async Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Users.ById(userId),
            null,
            cancellationToken);

        var exists = await ReturnFalseOnNotFoundAsync(response, $"get user '{userId}'", cancellationToken);
        if (!exists)
        {
            return null;
        }

        var result = await ReadJsonAsync<GetUserByIdResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetUserByIdResponse");
    }

    public async Task<GetUserByEmailResponse?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email must not be empty.", nameof(email));
        }

        var path = AppendQueryString(
            GuardhouseApiRoutes.Users.ByEmail(),
            [new KeyValuePair<string, string?>("email", email)]);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            path,
            null,
            cancellationToken);

        var exists = await ReturnFalseOnNotFoundAsync(response, $"get user by email '{email}'", cancellationToken);
        if (!exists)
        {
            return null;
        }

        var result = await ReadJsonAsync<GetUserByEmailResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetUserByEmailResponse");
    }

    public async Task<bool> UpdateUserAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Users.ById(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"update user '{userId}'", cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.PasswordChange(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"change password for user '{userId}'", cancellationToken);
    }

    public async Task<bool> RequestEmailChangeAsync(
        int userId,
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.EmailChange(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"request email change for user '{userId}'", cancellationToken);
    }

    public async Task<bool> AssignUserToRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Role(userId, roleId),
            null,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(
            response,
            $"assign role '{roleId}' to user '{userId}'",
            cancellationToken);
    }

    public async Task<bool> UnassignUserFromRoleAsync(
        int userId,
        int roleId,
        UnassignUserFromRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Users.Role(userId, roleId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(
            response,
            $"unassign role '{roleId}' from user '{userId}'",
            cancellationToken);
    }

    public async Task<bool> BlockUserAsync(int userId, BlockUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Patch,
            GuardhouseApiRoutes.Users.Block(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"block user '{userId}'", cancellationToken);
    }

    public async Task<bool> UnblockUserAsync(int userId, UnblockUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Patch,
            GuardhouseApiRoutes.Users.Unblock(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"unblock user '{userId}'", cancellationToken);
    }

    public async Task<bool> DeleteUserPersonalDataAsync(
        int userId,
        DeleteUserPersonalDataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Put,
            GuardhouseApiRoutes.Users.PersonalData(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"delete personal data for user '{userId}'", cancellationToken);
    }
}
