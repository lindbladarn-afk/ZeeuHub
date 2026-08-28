using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;
using WebApp.Services.PurchasePrice;

namespace WebApp.Tests;

// Verifies purchase price workbook parsing before services validate and stage rows.
public sealed class PurchasePriceWorkbookReaderTests
{
    [Fact]
    public async Task ReadAsync_Returns_NonEmpty_Rows_With_Canonical_Headers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PurchasePrice");
        WriteHeaders(sheet);
        sheet.Cell(2, 3).Value = "A-100";
        sheet.Cell(2, 4).Value = "10,50";
        sheet.Cell(4, 3).Value = "A-200";
        sheet.Cell(4, 4).Value = "20";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PurchasePriceValidation.WorkbookDefinition);

        Assert.Empty(result.Errors);
        Assert.Equal(PurchasePriceValidation.ExpectedHeaders, result.RowHeaders);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNo);
        Assert.Equal("A-100", result.Rows[0].Data["ArtNr"]);
        Assert.Equal(4, result.Rows[1].RowNo);
        Assert.Equal("20", result.Rows[1].Data["Inpris brutto valuta"]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Header_Errors()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PurchasePrice");
        WriteHeaders(sheet);
        sheet.Cell(1, 1).Value = "Fel";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PurchasePriceValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("Fel mall för vald importtyp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_Returns_Empty_File_Error()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("PurchasePrice");
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PurchasePriceValidation.WorkbookDefinition);

        Assert.Equal(["Filen innehåller inga rader att importera."], result.Errors);
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < PurchasePriceValidation.ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = PurchasePriceValidation.ExpectedHeaders[index];
        }
    }

    private static IFormFile CreateFormFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "purchaseprice.xlsx");
    }
}
