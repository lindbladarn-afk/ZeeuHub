using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.PriceUpdate;

namespace WebApp.Repositories.PriceUpdate
{
    public interface IPriceUpdateStagingRepository
    {
        Task BulkInsertAsync(IEnumerable<PortalPriceUpdateStagingRow> rows, CancellationToken cancellationToken = default);
    }
}
