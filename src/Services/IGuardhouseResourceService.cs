namespace Guardhouse.SDK.Services;

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Service interface for managing Guardhouse resource server operations.
/// </summary>
public interface IGuardhouseResourceService
{
    /// <summary>
    /// Validates a token and returns the claims principal if valid.
    /// Note: This method requires HttpContext context and will throw an InvalidOperationException if called directly.
    /// Use the built-in JWT bearer authentication for proper validation.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The claims principal if the token is valid, or null.</returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Introspects a token to determine if it is active.
    /// Note: Token introspection is handled by the authentication pipeline.
    /// Use IGuardhouseIntrospectionService directly if needed.
    /// </summary>
    /// <param name="token">The token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response.</returns>
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default authentication scheme configured for the application.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The default authentication scheme, or null if not configured.</returns>
    Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default);
}
