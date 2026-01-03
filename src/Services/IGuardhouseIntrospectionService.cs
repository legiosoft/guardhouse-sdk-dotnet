namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models;

/// <summary>
/// Service interface for introspecting tokens with the identity server.
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
}
