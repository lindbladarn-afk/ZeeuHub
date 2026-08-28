using System.Text.Json;
using WebApp.Models.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate;

// Creates staging rows from validated price update import data.
public interface IPriceUpdateStagingRowFactory
{
    PortalPriceUpdateStagingRow Create(PriceUpdateStagingRowCreateRequest request);
}

public sealed class PriceUpdateStagingRowCreateRequest
{
    public Guid ImportBatchId { get; init; }
    public int RowNo { get; init; }
    public IReadOnlyDictionary<string, string> RawJsonData { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public JsonSerializerOptions? JsonOptions { get; init; }
    public string ImportedBy { get; init; } = string.Empty;
    public ExcelImportUserContext? UserContext { get; init; }
}
