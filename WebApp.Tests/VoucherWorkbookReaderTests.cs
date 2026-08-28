using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using WebApp.Services.Vouchers;

namespace WebApp.Tests;

// Verifies shared workbook parsing for voucher import flows.
public sealed class VoucherWorkbookReaderTests
{
    [Fact]
    public async Task ReadAsync_Returns_NonEmpty_Rows_With_Canonical_Headers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Statistic");
        WriteHeaders(sheet);
        sheet.Cell(2, 1).Value = "6550";
        sheet.Cell(2, 9).Value = "100,50";
        sheet.Cell(2, 12).Value = "Office cost";
        sheet.Cell(3, 1).Value = string.Empty;
        sheet.Cell(4, 1).Value = "4000";
        sheet.Cell(4, 10).Value = "25";

        var reader = new VoucherWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook));

        Assert.Empty(result.Errors);
        Assert.Equal(VoucherValidation.ExpectedHeaders, result.RowHeaders);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNo);
        Assert.Equal("6550", result.Rows[0].Data["Account"]);
        Assert.Equal("100,50", result.Rows[0].Data["Debit"]);
        Assert.Equal(4, result.Rows[1].RowNo);
        Assert.Equal("25", result.Rows[1].Data["Credit"]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Header_Errors()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Statistic");
        WriteHeaders(sheet);
        sheet.Cell(1, 1).Value = "Wrong";

        var reader = new VoucherWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook));

        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Rows);
        Assert.Contains("Fel rubrik i kolumn 1", result.Errors[0]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Empty_File_Error()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Statistic");

        var reader = new VoucherWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook));

        Assert.Empty(result.Rows);
        Assert.Equal(new[] { "Filen innehåller inga rader att importera." }, result.Errors);
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < VoucherValidation.ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = VoucherValidation.ExpectedHeaders[index];
        }
    }

    private static IFormFile CreateFormFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "file", "voucher.xlsx");
    }
}
