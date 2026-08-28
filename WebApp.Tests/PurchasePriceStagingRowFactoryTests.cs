using WebApp.Services.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the shared mapping from purchase price rows to Jeeves staging rows.
public sealed class PurchasePriceStagingRowFactoryTests
{
    [Fact]
    public void Create_Maps_Purchase_Price_Data_To_Staging_Row()
    {
        var batchId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var rawData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ArtNr"] = "A-100",
            ["Inpris brutto valuta"] = "10,50"
        };
        var factory = new PurchasePriceStagingRowFactory();

        var row = factory.Create(new PurchasePriceStagingRowCreateRequest
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
        Assert.Contains("\"ArtNr\":\"A-100\"", row.RawJson);
        Assert.True(row.ImportedAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
