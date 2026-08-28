using System;

namespace WebApp.Models.SupplierPrice;

// Represents one normalized supplier price row staged from a customer-specific price-list import.
public sealed class PortalSupplierPriceStagingRow
{
    public Guid ImportBatchId { get; set; }
    public int RowNo { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierArticleNo { get; set; }
    public string? CustomerArticleNo { get; set; }
    public string? Description { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? ListPrice { get; set; }
    public decimal? NetPrice { get; set; }
    public decimal? DiscountPercent { get; set; }
    public string? Uom { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public decimal? PackageQuantity { get; set; }
    public decimal? WeightKg { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? TariffCode { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Category1 { get; set; }
    public string? Category2 { get; set; }
    public string? Category3 { get; set; }
    public string? Category4 { get; set; }
    public string? Category5 { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceSheetName { get; set; }
    public int? SourceRowNo { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public int? ForetagKod { get; set; }
    public string? UserId { get; set; }
}
