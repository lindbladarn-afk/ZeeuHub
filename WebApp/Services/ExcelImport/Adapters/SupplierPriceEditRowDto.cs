namespace WebApp.Services.ExcelImport;

// Carries one normalized supplier-price row from the browser to the edit adapter.
public sealed class SupplierPriceEditRowDto
{
    public int RowNo { get; set; }
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
