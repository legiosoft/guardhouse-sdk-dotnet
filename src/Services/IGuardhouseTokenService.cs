using Guardhouse.SDK.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Guardhouse.SDK.Services;

/// <summary>
/// Service for managing OAuth tokens with Guardhouse
/// </summary>
public interface IGuardhouseTokenService
{
    /// <summary>
    /// Gets a valid access token, refreshing if necessary
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a new token from the Guardhouse server
    /// </summary>
    Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an existing token using the refresh token
    /// </summary>
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Introspects a token to validate it
    /// </summary>
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a token is still active
    /// </summary>
    Task<bool> IsTokenActiveAsync(string token, CancellationToken cancellationToken = default);
}