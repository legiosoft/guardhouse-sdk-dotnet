namespace Guardhouse.SDK.Services;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Service for managing Guardhouse resource server operations.
/// </summary>
public class GuardhouseResourceService(IAuthenticationSchemeProvider schemeProvider)
    : IGuardhouseResourceService
{
    /// <summary>
    /// Validates a token and returns the claims principal if valid.
    /// Note: This method requires HttpContext context and will throw an InvalidOperationException if called directly.
    /// Use the built-in JWT bearer authentication for proper validation.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The claims principal if the token is valid, or null.</returns>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Token validation requires HttpContext context. Use built-in JWT bearer authentication for proper validation.");
    }

    /// <summary>
    /// Introspects a token to determine if it is active.
    /// Note: Token introspection is handled by the authentication pipeline.
    /// Use IGuardhouseIntrospectionService directly if needed.
    /// </summary>
    /// <param name="token">The token to introspect.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An introspection response.</returns>
    public Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Token introspection is handled by the authentication pipeline. Use IGuardhouseIntrospectionService directly if needed.");
    }

    /// <summary>
    /// Gets the default authentication scheme configured for the application.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The default authentication scheme, or null if not configured.</returns>
    public async Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default)
    {
        var scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
        return scheme;
    }
}
