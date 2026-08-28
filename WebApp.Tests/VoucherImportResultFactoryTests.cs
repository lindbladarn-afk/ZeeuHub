using WebApp.Services.ExcelImport;
using WebApp.Services.Vouchers;

namespace WebApp.Tests;

// Verifies voucher import result shaping before controllers render or serialize it.
public sealed class VoucherImportResultFactoryTests
{
    [Fact]
    public void CreateImportResult_Formats_Dates_And_Copies_Collections()
    {
        var rowHeaders = new List<string> { "Account" };
        var errors = new List<string> { "Rad 2: Konto saknas" };
        var rowResults = new List<ExcelImportRowResult>
        {
            new()
            {
                RowNo = 2,
                IsValid = false,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }
        };
        var factory = new VoucherImportResultFactory();

        var result = factory.CreateImportResult(new VoucherImportResultCreateRequest
        {
            ImportBatchId = Guid.NewGuid(),
            TotalRows = 1,
            ValidRows = 0,
            PostingDate = new DateTime(2026, 4, 27),
            ReversalDate = new DateTime(2026, 5, 1),
            RowHeaders = rowHeaders,
            RowResults = rowResults,
            Errors = errors
        });

        rowHeaders.Add("Debit");
        errors.Add("extra");
        rowResults.Clear();

        Assert.Equal("2026-04-27", result.VoucherPostingDate);
        Assert.Equal("2026-05-01", result.VoucherReversalDate);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(["Account"], result.RowHeaders);
        Assert.Single(result.RowResults);
        Assert.Equal(["Rad 2: Konto saknas"], result.Errors);
    }

    [Fact]
    public void ToExcelImportResult_Uses_Voucher_Result_Without_Recalculating_Behavior()
    {
        var factory = new VoucherImportResultFactory();
        var voucherResult = factory.CreateImportResult(new VoucherImportResultCreateRequest
        {
            ImportBatchId = Guid.NewGuid(),
            TotalRows = 3,
            ValidRows = 2,
            RowHeaders = new[] { "Account" },
            RowResults = new[]
            {
                new ExcelImportRowResult
                {
                    RowNo = 1,
                    IsValid = true,
                    Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                }
            }
        });

        var result = factory.ToExcelImportResult(voucherResult, "voucher");

        Assert.Equal("voucher", result.ImportType);
        Assert.Equal(voucherResult.ImportBatchId, result.ImportBatchId);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(voucherResult.RowHeaders, result.RowHeaders);
        Assert.Equal(voucherResult.RowResults, result.RowResults);
    }
}
