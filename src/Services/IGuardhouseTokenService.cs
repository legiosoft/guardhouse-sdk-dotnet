namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models;

/// <summary>
/// Service interface for obtaining and managing access tokens from the identity server.
/// </summary>
public interface IGuardhouseTokenService
{
    /// <summary>
    /// Gets an access token, using cached tokens or requesting a new one as needed.
    /// This method automatically handles token caching, refresh, and retry logic.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A valid access token.</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a new access token from the identity server using client credentials grant.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A token response containing the access token and related information.</returns>
    Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use for obtaining a new access token.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A token response containing the new access token and related information.</returns>
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Introspects a token to determine if it is active and retrieve its claims.
    /// Uses client credentials for authentication to the introspection endpoint.
    /// </summary>
    /// <param name="token">The token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response containing the token's status and claims.</returns>
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a token is currently active.
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the token is active, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown when introspection fails.</exception>
    Task<bool> IsTokenActiveAsync(string token, CancellationToken cancellationToken = default);
}
