using System.Security.Claims;
using Guardhouse.SDK.Models;
using Microsoft.AspNetCore.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Guardhouse.SDK.Services;

/// <summary>
/// Service for Guardhouse resource server operations
/// </summary>
public interface IGuardhouseResourceService
{
    /// <summary>
    /// Validates a token and returns user information
    /// </summary>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Introspects a token using the resource server configuration
    /// </summary>
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authentication scheme handler for token validation
    /// </summary>
    Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default);
}