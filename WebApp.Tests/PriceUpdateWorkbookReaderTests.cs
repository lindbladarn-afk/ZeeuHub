using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;
using WebApp.Services.PriceUpdate;

namespace WebApp.Tests;

// Verifies price update workbook parsing before services validate and stage rows.
public sealed class PriceUpdateWorkbookReaderTests
{
    [Fact]
    public async Task ReadAsync_Returns_NonEmpty_Rows_With_Canonical_Headers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PriceUpdate");
        WriteHeaders(sheet);
        sheet.Cell(2, 1).Value = "A-100";
        sheet.Cell(2, 2).Value = "10,50";
        sheet.Cell(4, 1).Value = "A-200";
        sheet.Cell(4, 2).Value = "20";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PriceUpdateValidation.WorkbookDefinition);

        Assert.Empty(result.Errors);
        Assert.Equal(PriceUpdateValidation.ExpectedHeaders, result.RowHeaders);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNo);
        Assert.Equal("A-100", result.Rows[0].Data["Artnr"]);
        Assert.Equal(4, result.Rows[1].RowNo);
        Assert.Equal("20", result.Rows[1].Data["Pris"]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Header_Errors()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PriceUpdate");
        WriteHeaders(sheet);
        sheet.Cell(1, 1).Value = "Fel";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PriceUpdateValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("Fel mall för vald importtyp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_Returns_Empty_File_Error()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("PriceUpdate");
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), PriceUpdateValidation.WorkbookDefinition);

        Assert.Equal(["Filen innehåller inga rader att importera."], result.Errors);
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < PriceUpdateValidation.ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = PriceUpdateValidation.ExpectedHeaders[index];
        }
    }

    private static IFormFile CreateFormFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "priceupdate.xlsx");
    }
}
