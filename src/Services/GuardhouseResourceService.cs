using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Guardhouse.SDK.Models;

namespace Guardhouse.SDK.Services;

public class GuardhouseResourceService : IGuardhouseResourceService
{
    private readonly IOptions<GuardhouseResourceOptions> _options;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly ILogger<GuardhouseResourceService> _logger;

    public GuardhouseResourceService(
        IOptions<GuardhouseResourceOptions> options,
        IAuthenticationSchemeProvider schemeProvider,
        ILogger<GuardhouseResourceService>? logger = null)
    {
        _options = options;
        _schemeProvider = schemeProvider;
        _logger = logger ?? NullLogger<GuardhouseResourceService>.Instance;
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Token validation requires HttpContext context. Use built-in JWT bearer authentication for proper validation.");
        return null;
    }

    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Token introspection is handled by the authentication pipeline. Use IGuardhouseIntrospectionService directly if needed.");
        throw new InvalidOperationException("Use IGuardhouseIntrospectionService for introspection.");
    }

    public async Task<AuthenticationScheme?> GetDefaultSchemeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var scheme = await _schemeProvider.GetDefaultAuthenticateSchemeAsync();
            if (scheme == null)
            {
                _logger.LogError("No default authentication scheme found");
            }
            return scheme;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get default authentication scheme");
            return null;
        }
    }
}
