using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.PurchasePrice;

namespace WebApp.Repositories.PurchasePrice
{
    public interface IPurchasePriceStagingRepository
    {
        Task BulkInsertAsync(IEnumerable<PortalPurchasePriceStagingRow> rows, CancellationToken cancellationToken = default);
    }
}
