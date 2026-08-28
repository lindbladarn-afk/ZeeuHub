using Entities.Purchase;
using WebApp.Models.Purchase.Demo;

namespace WebApp.Services.Purchase.Demo;

// Loads the bundled purchase demo payloads from disk.
public interface IPurchaseDemoDataService
{
    Task<PurchaseDemoData> LoadAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrderVM?> FindOrderAsync(int orderNumber, CancellationToken cancellationToken = default);
}
