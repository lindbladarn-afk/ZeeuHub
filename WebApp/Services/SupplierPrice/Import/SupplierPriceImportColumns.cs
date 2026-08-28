namespace WebApp.Services.SupplierPrice;

// Defines the normalized supplier-price columns shared by imports, results, and edit sessions.
public static class SupplierPriceImportColumns
{
    public static readonly IReadOnlyList<string> ResultHeaders =
    [
        "Supplier",
        "SupplierArticleNo",
        "CustomerArticleNo",
        "Description",
        "CurrencyCode",
        "ListPrice",
        "NetPrice",
        "DiscountPercent",
        "Uom",
        "MinimumOrderQuantity",
        "PackageQuantity",
        "WeightKg",
        "CountryOfOrigin",
        "TariffCode",
        "ValidFrom",
        "ValidTo",
        "Category1",
        "Category2",
        "Category3",
        "Category4",
        "Category5",
        "SourceSheetName",
        "SourceRowNo"
    ];
}
