namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models;

/// <summary>
/// Service interface for introspecting tokens with the identity server.
/// This service should be used by resource servers to validate incoming tokens.
/// </summary>
public interface IGuardhouseIntrospectionService
{
    /// <summary>
    /// Introspects a token to determine if it is active and retrieve its claims.
    /// </summary>
    /// <param name="token">The access token to introspect.</param>
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
