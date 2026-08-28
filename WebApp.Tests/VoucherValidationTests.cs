using ClosedXML.Excel;
using WebApp.Services.Vouchers;

namespace WebApp.Tests;

public sealed class VoucherValidationTests
{
    [Fact]
    public void ValidateHeaders_AcceptsCurrentVoucherTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Statistic");

        for (var i = 0; i < VoucherValidation.ExpectedHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = VoucherValidation.ExpectedHeaders[i];
        }

        var errors = new List<string>();
        var isValid = VoucherValidation.ValidateHeaders(sheet.Row(1), errors);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateHeaders_AcceptsLegacyVoucherAliases()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Statistic");
        var legacyHeaders = new[]
        {
            "Account",
            "Koststalle",
            "Kostbar",
            "K4",
            "K5",
            "K6",
            "K7",
            "Project",
            "Debbel",
            "Krebel",
            "VAT Code",
            "Voucher Text",
            "Posting Template"
        };

        for (var i = 0; i < legacyHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = legacyHeaders[i];
        }

        var errors = new List<string>();
        var isValid = VoucherValidation.ValidateHeaders(sheet.Row(1), errors);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRowData_RequiresDebitOrCredit()
    {
        var rowData = VoucherValidation.ExpectedHeaders.ToDictionary(
            header => header,
            _ => string.Empty,
            StringComparer.OrdinalIgnoreCase);
        rowData["Account"] = "6550";

        var errors = VoucherValidation.ValidateRowData(rowData).ToList();

        Assert.Contains("Debit eller Credit måste anges.", errors);
    }
}
