using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration
{
    public interface IOrderSourceClient
    {
        IntegrationSource Source { get; }
        Task<IReadOnlyList<IntegrationOrder>> FetchOrdersAsync(IntegrationFetchRequest request, CancellationToken ct = default);
    }
}
