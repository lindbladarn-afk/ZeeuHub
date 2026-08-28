using Entities.Purchase;

namespace WebApp.ViewModels.Purchase;

// Shapes Purchase autocomplete responses consumed by the order entry view.
public sealed class PurchaseSupplierLookupItem
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string SupplierNumber { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string? SupplierCo { get; init; }
    public string? SupplierStreet { get; init; }
    public string? SupplierZipCode { get; init; }
    public string? SupplierCity { get; init; }
    public string? SupplierCountry { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? DeliveryCompany { get; init; }
    public string? DeliveryCo { get; init; }
    public string? DeliveryStreet { get; init; }
    public string? DeliveryZipCode { get; init; }
    public string? DeliveryCity { get; init; }
    public string? DeliveryCountry { get; init; }
    public IReadOnlyList<IPurchaseSupplierContactVM> Contacts { get; init; } = Array.Empty<IPurchaseSupplierContactVM>();
}

public sealed class PurchaseArticleLookupItem
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string ArticleNumber { get; init; } = string.Empty;
    public string ArticleDescription { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public int ProductGroupCode { get; init; }
    public string DefaultAccount { get; init; } = string.Empty;
    public string DefaultCostCenter { get; init; } = string.Empty;
    public bool ExpenceArticle { get; init; }
}
