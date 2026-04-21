namespace Guardhouse.SDK.Services;

using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;

public class GuardhouseUserService(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger<GuardhouseUserService>? logger = null)
    : GuardhouseUsersClient(httpClient, tokenService, userOptions, clientOptions, logger), IGuardhouseUserService;
