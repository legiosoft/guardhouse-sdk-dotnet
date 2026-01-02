namespace Guardhouse.SDK.Services;

using System.Threading;
using System.Threading.Tasks;
using Models;

public interface IGuardhouseIntrospectionService
{
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
}
