namespace Guardhouse.SDK.Services;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Models;
using Microsoft.AspNetCore.Authentication;

public class GuardhouseResourceService(IAuthenticationSchemeProvider schemeProvider)
    : IGuardhouseResourceService
{
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Token validation requires HttpContext context. Use built-in JWT bearer authentication for proper validation.");
    }

    public Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Token introspection is handled by the authentication pipeline. Use IGuardhouseIntrospectionService directly if needed.");
    }

    public async Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default)
    {
        var scheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
        return scheme;
    }
}
