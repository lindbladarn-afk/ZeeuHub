using WebApp.Services.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the shared mapping from price update rows to Jeeves staging rows.
public sealed class PriceUpdateStagingRowFactoryTests
{
    [Fact]
    public void Create_Maps_Price_Update_Data_To_Staging_Row()
    {
        var batchId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var rawData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Artnr"] = "A-100",
            ["Pris"] = "10,50"
        };
        var factory = new PriceUpdateStagingRowFactory();

        var row = factory.Create(new PriceUpdateStagingRowCreateRequest
        {
            ImportBatchId = batchId,
            RowNo = 5,
            RawJsonData = rawData,
            ImportedBy = "alex@example.com",
            UserContext = new ExcelImportUserContext
            {
                CompanyId = companyId,
                ForetagKod = 100,
                UserId = "user-1"
            }
        });

        Assert.Equal(batchId, row.ImportBatchId);
        Assert.Equal(5, row.RowNo);
        Assert.Equal("alex@example.com", row.ImportedBy);
        Assert.Equal(companyId, row.CompanyId);
        Assert.Equal(100, row.ForetagKod);
        Assert.Equal("user-1", row.UserId);
        Assert.Contains("\"Artnr\":\"A-100\"", row.RawJson);
        Assert.True(row.ImportedAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
