namespace Guardhouse.SDK.Services;

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Microsoft.AspNetCore.Authentication;

public interface IGuardhouseResourceService
{
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default);
}
