using System.Text.Json;
using WebApp.Models.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget;

// Creates staging rows from validated budget import data.
public interface IBudgetStagingRowFactory
{
    PortalBudgetStagingRow Create(BudgetStagingRowCreateRequest request);
}

public sealed class BudgetStagingRowCreateRequest
{
    public Guid ImportBatchId { get; init; }
    public int RowNo { get; init; }
    public IReadOnlyDictionary<string, string> RawJsonData { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public JsonSerializerOptions? JsonOptions { get; init; }
    public string ImportedBy { get; init; } = string.Empty;
    public ExcelImportUserContext? UserContext { get; init; }
}
