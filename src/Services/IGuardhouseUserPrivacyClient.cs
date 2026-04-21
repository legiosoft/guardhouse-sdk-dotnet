namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models.Users.Privacy;

/// <summary>
/// Client interface for Guardhouse user privacy and personal-data operations.
/// </summary>
public interface IGuardhouseUserPrivacyClient
{
    Task<bool> DeletePersonalDataAsync(
        int userId,
        DeleteUserPersonalDataRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<bool> AnonymizePersonalDataAsync(
        int userId,
        AnonymizeUserPersonalDataRequest? request = null,
        CancellationToken cancellationToken = default);
}
