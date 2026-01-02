namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models;

public interface IGuardhouseTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> IsTokenActiveAsync(string token, CancellationToken cancellationToken = default);
}
