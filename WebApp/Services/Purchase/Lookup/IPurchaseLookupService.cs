using WebApp.ViewModels.Purchase;

namespace WebApp.Services.Purchase.Lookup;

// Provides Purchase lookup results for autocomplete endpoints.
public interface IPurchaseLookupService
{
    Task<IReadOnlyList<PurchaseSupplierLookupItem>> SearchSuppliersAsync(string? searchString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseArticleLookupItem>> SearchArticlesAsync(string? searchString, CancellationToken cancellationToken = default);
}
