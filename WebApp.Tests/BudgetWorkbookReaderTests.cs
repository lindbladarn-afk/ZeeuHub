using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;
using WebApp.Services.Budget;

namespace WebApp.Tests;

// Verifies budget workbook parsing before services validate and stage rows.
public sealed class BudgetWorkbookReaderTests
{
    [Fact]
    public async Task ReadAsync_Returns_NonEmpty_Rows_With_Canonical_Headers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Budget");
        WriteHeaders(sheet);
        sheet.Cell(2, 1).Value = "3000";
        sheet.Cell(2, 11).Value = "1000";
        sheet.Cell(4, 1).Value = "4000";
        sheet.Cell(4, 11).Value = "2000";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), BudgetValidation.WorkbookDefinition);

        Assert.Empty(result.Errors);
        Assert.Equal(BudgetValidation.ExpectedHeaders, result.RowHeaders);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNo);
        Assert.Equal("3000", result.Rows[0].Data["Account"]);
        Assert.Equal(4, result.Rows[1].RowNo);
        Assert.Equal("2000", result.Rows[1].Data["Amount"]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Header_Errors()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Budget");
        WriteHeaders(sheet);
        sheet.Cell(1, 1).Value = "Fel";
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), BudgetValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("Fel mall för vald importtyp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_Returns_Empty_File_Error()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Budget");
        var reader = new ExcelImportWorkbookReader();

        var result = await reader.ReadAsync(CreateFormFile(workbook), BudgetValidation.WorkbookDefinition);

        Assert.Equal(["Filen innehåller inga rader att importera."], result.Errors);
    }

    [Fact]
    public async Task ReadAsync_Returns_Safe_Error_For_Invalid_Workbook()
    {
        var reader = new ExcelImportWorkbookReader();
        var bytes = System.Text.Encoding.UTF8.GetBytes("not an excel workbook");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.xlsx");

        var result = await reader.ReadAsync(file, BudgetValidation.WorkbookDefinition);

        Assert.Single(result.Errors);
        Assert.Contains("Filen kunde inte läsas", result.Errors[0], StringComparison.Ordinal);
        Assert.Equal(BudgetValidation.ExpectedHeaders, result.RowHeaders);
    }

    [Fact]
    public async Task ReadAsync_Reads_Csv_With_Semicolon_Delimiter()
    {
        var reader = new ExcelImportWorkbookReader();
        var csv = string.Join(';', BudgetValidation.ExpectedHeaders)
                  + "\n6110;5999;;;;;;;5;;5000";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.csv");

        var result = await reader.ReadAsync(file, BudgetValidation.WorkbookDefinition);

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
        Assert.Equal(2, result.Rows[0].RowNo);
        Assert.Equal("6110", result.Rows[0].Data["Account"]);
        Assert.Equal("5000", result.Rows[0].Data["Amount"]);
    }

    [Fact]
    public async Task ReadAsync_Returns_Header_Error_For_Csv_With_Extra_Header()
    {
        var reader = new ExcelImportWorkbookReader();
        var csv = string.Join(';', BudgetValidation.ExpectedHeaders.Concat(new[] { "Extra" }))
                  + "\n6110;5999;;;;;;;5;;5000;nope";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.csv");

        var result = await reader.ReadAsync(file, BudgetValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("fler kolumnrubriker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_Returns_Safe_Error_For_Invalid_Legacy_Xls()
    {
        var reader = new ExcelImportWorkbookReader();
        var bytes = System.Text.Encoding.UTF8.GetBytes("not a binary xls workbook");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.xls");

        var result = await reader.ReadAsync(file, BudgetValidation.WorkbookDefinition);

        Assert.Single(result.Errors);
        Assert.Contains(".xls-fil", result.Errors[0], StringComparison.Ordinal);
        Assert.Equal(BudgetValidation.ExpectedHeaders, result.RowHeaders);
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < BudgetValidation.ExpectedHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = BudgetValidation.ExpectedHeaders[index];
        }
    }

    private static IFormFile CreateFormFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "budget.xlsx");
    }
}
