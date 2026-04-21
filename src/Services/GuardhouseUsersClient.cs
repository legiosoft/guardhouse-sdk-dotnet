namespace Guardhouse.SDK.Services;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Users;

public class GuardhouseUsersClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseUserManagementClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
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

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictDetails = await ReadSafeResponseSummaryAsync(response, cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create user. Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                conflictDetails);
        }

        await EnsureSuccessStatusCodeAsync(response, "create user", cancellationToken);

        var result = await ReadJsonAsync<CreateUserResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize CreateUserResponse");
    }

    public async Task<GetUserByIdResponse?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Get,
            GuardhouseApiRoutes.Users.ById(userId),
            null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(response, $"get user by id '{userId}'", cancellationToken);

        var result = await ReadJsonAsync<GetUserByIdResponse>(response, cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GetUserByIdResponse");
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

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Users.ById(userId),
            null,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"delete user '{userId}'", cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Password(userId),
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var badRequestDetails = await ReadSafeResponseSummaryAsync(response, cancellationToken);
            throw new InvalidOperationException(
                $"Failed to change user password. Status: {response.StatusCode} ({(int)response.StatusCode}). " +
                badRequestDetails);
        }

        await EnsureSuccessStatusCodeAsync(response, $"change password for user '{userId}'", cancellationToken);
        return true;
    }
}
