using System.Text.Json;
using WebApp.Models.Budget;

namespace WebApp.Services.Budget;

// Maps budget import data to the portal staging row used by Jeeves import.
public sealed class BudgetStagingRowFactory : IBudgetStagingRowFactory
{
    public PortalBudgetStagingRow Create(BudgetStagingRowCreateRequest request)
    {
        var jsonOptions = request.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return new PortalBudgetStagingRow
        {
            ImportBatchId = request.ImportBatchId,
            RowNo = request.RowNo,
            RawJson = JsonSerializer.Serialize(request.RawJsonData, jsonOptions),
            ImportedAt = DateTime.UtcNow,
            ImportedBy = request.ImportedBy,
            CompanyId = request.UserContext?.CompanyId,
            ForetagKod = request.UserContext?.ForetagKod,
            UserId = request.UserContext?.UserId
        };
    }
}
