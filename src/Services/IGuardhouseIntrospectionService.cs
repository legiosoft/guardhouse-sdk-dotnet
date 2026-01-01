using System.Threading;
using System.Threading.Tasks;
using Guardhouse.SDK.Models;

namespace Guardhouse.SDK.Services;

public interface IGuardhouseIntrospectionService
{
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, CancellationToken cancellationToken = default);
}
