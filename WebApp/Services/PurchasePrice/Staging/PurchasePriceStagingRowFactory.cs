using System.Text.Json;
using WebApp.Models.PurchasePrice;

namespace WebApp.Services.PurchasePrice;

// Maps purchase price import data to the portal staging row used by Jeeves import.
public sealed class PurchasePriceStagingRowFactory : IPurchasePriceStagingRowFactory
{
    public PortalPurchasePriceStagingRow Create(PurchasePriceStagingRowCreateRequest request)
    {
        var jsonOptions = request.JsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return new PortalPurchasePriceStagingRow
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
