namespace Guardhouse.SDK.Services;

using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;

public abstract class GuardhouseUserManagementClientBase(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseApiClientBase(httpClient, tokenService, userOptions, clientOptions, logger);
