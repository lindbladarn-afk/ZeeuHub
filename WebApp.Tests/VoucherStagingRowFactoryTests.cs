using WebApp.Services.ExcelImport;
using WebApp.Services.Vouchers;

namespace WebApp.Tests;

// Verifies the shared mapping from voucher import rows to Jeeves staging rows.
public sealed class VoucherStagingRowFactoryTests
{
    [Fact]
    public void Create_Maps_Voucher_Row_Data_To_Staging_Row()
    {
        var batchId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var rowData = VoucherValidation.ExpectedHeaders.ToDictionary(
            header => header,
            _ => string.Empty,
            StringComparer.OrdinalIgnoreCase);
        rowData["Account"] = "6550";
        rowData["Cost center"] = "100";
        rowData["Cost unit"] = "200";
        rowData["K4"] = "K4";
        rowData["Project"] = "P1";
        rowData["Debit"] = "123,45";
        rowData["VAT code"] = "25";
        rowData["Voucher text"] = "Office cost";
        rowData["Allocation"] = "ALLOC";
        var factory = new VoucherStagingRowFactory();

        var row = factory.Create(new VoucherStagingRowCreateRequest
        {
            ImportBatchId = batchId,
            RowNo = 7,
            RowData = rowData,
            RawJsonData = rowData,
            ImportedBy = "alex@example.com",
            UserContext = new ExcelImportUserContext
            {
                CompanyId = companyId,
                ForetagKod = 100,
                UserId = "user-1"
            },
            PostingDate = new DateTime(2026, 4, 27),
            ReversalDate = new DateTime(2026, 5, 1)
        });

        Assert.Equal(batchId, row.ImportBatchId);
        Assert.Equal(7, row.RowNo);
        Assert.Equal("6550", row.Account);
        Assert.Equal("6550", row.Ktonr);
        Assert.Equal("100", row.Koststallekod);
        Assert.Equal("200", row.Kostbar);
        Assert.Equal("K4", row.K4);
        Assert.Equal("P1", row.Projcode);
        Assert.Equal("123,45", row.Amount);
        Assert.Equal("123,45", row.Debbel);
        Assert.Equal("123,45", row.Vbbelopp);
        Assert.Equal("25", row.Momskod);
        Assert.Equal("Office cost", row.VoucherText);
        Assert.Equal("ALLOC", row.Autoregel);
        Assert.Equal("alex@example.com", row.ImportedBy);
        Assert.Equal(companyId, row.CompanyId);
        Assert.Equal(100, row.ForetagKod);
        Assert.Equal("user-1", row.UserId);
        Assert.Equal(new DateTime(2026, 4, 27), row.PostingDate);
        Assert.Equal(new DateTime(2026, 5, 1), row.AterBokfDat);
        Assert.Contains("\"Account\":\"6550\"", row.RawJson);
    }
}
