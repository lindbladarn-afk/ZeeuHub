using WebApp.Models.SupplierPrice;
using WebApp.Services.SupplierPrice;

namespace WebApp.Repositories.PressKogyoPrice;

// Persists normalized Press Kogyo price rows to the dedicated staging database table.
public interface IPressKogyoPriceStagingRepository : ISupplierPriceStagingRepository
{
    new Task BulkInsertAsync(
        IEnumerable<PortalSupplierPriceStagingRow> rows,
        CancellationToken cancellationToken = default);
}
