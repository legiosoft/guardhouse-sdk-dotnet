namespace Guardhouse.SDK.Services;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Users.Privacy;

public class GuardhouseUserPrivacyClient(
    HttpClient httpClient,
    IGuardhouseTokenService tokenService,
    IOptions<GuardhouseUserOptions> userOptions,
    IOptions<GuardhouseClientOptions> clientOptions,
    ILogger? logger = null)
    : GuardhouseUserManagementClientBase(httpClient, tokenService, userOptions, clientOptions, logger),
        IGuardhouseUserPrivacyClient
{
    public async Task<bool> DeletePersonalDataAsync(
        int userId,
        DeleteUserPersonalDataRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Delete,
            GuardhouseApiRoutes.Users.PersonalData(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"delete personal data for user '{userId}'", cancellationToken);
    }

    public async Task<bool> AnonymizePersonalDataAsync(
        int userId,
        AnonymizeUserPersonalDataRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRequestAsync(
            HttpMethod.Post,
            GuardhouseApiRoutes.Users.Anonymize(userId),
            request,
            cancellationToken);

        return await ReturnFalseOnNotFoundAsync(response, $"anonymize personal data for user '{userId}'", cancellationToken);
    }
}
