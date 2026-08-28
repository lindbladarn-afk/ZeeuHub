using System.Text.Json;
using WebApp.Models.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice;

// Creates staging rows from validated purchase price import data.
public interface IPurchasePriceStagingRowFactory
{
    PortalPurchasePriceStagingRow Create(PurchasePriceStagingRowCreateRequest request);
}

public sealed class PurchasePriceStagingRowCreateRequest
{
    public Guid ImportBatchId { get; init; }
    public int RowNo { get; init; }
    public IReadOnlyDictionary<string, string> RawJsonData { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public JsonSerializerOptions? JsonOptions { get; init; }
    public string ImportedBy { get; init; } = string.Empty;
    public ExcelImportUserContext? UserContext { get; init; }
}
