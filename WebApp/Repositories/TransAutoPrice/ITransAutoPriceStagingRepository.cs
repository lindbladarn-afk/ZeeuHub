using WebApp.Models.SupplierPrice;
using WebApp.Services.SupplierPrice;

namespace WebApp.Repositories.TransAutoPrice;

// Persists normalized Trans Auto supplier price rows to the Jeeves staging database.
public interface ITransAutoPriceStagingRepository : ISupplierPriceStagingRepository
{
    new Task BulkInsertAsync(
        IEnumerable<PortalSupplierPriceStagingRow> rows,
        CancellationToken cancellationToken = default);
}
