using System.Text.Json;
using WebApp.Models.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate;

// Maps price update import data to the portal staging row used by Jeeves import.
public sealed class PriceUpdateStagingRowFactory : IPriceUpdateStagingRowFactory
{
    public PortalPriceUpdateStagingRow Create(PriceUpdateStagingRowCreateRequest request)
    {
        var jsonOptions = request.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return new PortalPriceUpdateStagingRow
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
