using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using WebApp.Services.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies that streamed workbook parsers enforce defensive resource limits.
public sealed class ExcelImportResourceLimitTests
{
    [Fact]
    public async Task OpenXmlReader_Rejects_Too_Many_Columns()
    {
        using var workbook = CreateBudgetWorkbook();
        workbook.Worksheet(1).Cell(2, ExcelImportResourceLimits.MaxColumns + 1).Value = "unexpected";

        var result = await new ExcelImportWorkbookReader().ReadAsync(
            CreateFormFile(workbook, "budget.xlsx"),
            BudgetValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("kolumner", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task OpenXmlReader_Rejects_Oversized_Cell()
    {
        using var workbook = CreateBudgetWorkbook();
        workbook.Worksheet(1).Cell(2, 1).Value = new string('A', ExcelImportResourceLimits.MaxCellLength + 1);

        var result = await new ExcelImportWorkbookReader().ReadAsync(
            CreateFormFile(workbook, "budget.xlsx"),
            BudgetValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("cell är för lång", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task CsvReader_Rejects_Too_Many_Columns()
    {
        var headers = string.Join(';', BudgetValidation.ExpectedHeaders);
        var row = string.Join(';', Enumerable.Repeat("value", ExcelImportResourceLimits.MaxColumns + 1));
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{headers}\n{row}");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.csv");

        var result = await new ExcelImportWorkbookReader().ReadAsync(file, BudgetValidation.WorkbookDefinition);

        Assert.Contains(result.Errors, error => error.Contains("kolumner", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task OpenXmlReader_Observes_Cancellation()
    {
        using var workbook = CreateBudgetWorkbook();
        workbook.Worksheet(1).Cell(2, 1).Value = "3000";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ExcelImportWorkbookReader().ReadAsync(
                CreateFormFile(workbook, "budget.xlsx"),
                BudgetValidation.WorkbookDefinition,
                cancellation.Token));
    }

    [Fact]
    public async Task LegacyXlsReader_Observes_Cancellation_BeforeParsing()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "budget.xls");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ExcelImportWorkbookReader().ReadAsync(
                file,
                BudgetValidation.WorkbookDefinition,
                cancellation.Token));
    }

    private static XLWorkbook CreateBudgetWorkbook()
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Budget");
        for (var index = 0; index < BudgetValidation.ExpectedHeaders.Length; index++)
            sheet.Cell(1, index + 1).Value = BudgetValidation.ExpectedHeaders[index];

        return workbook;
    }

    private static IFormFile CreateFormFile(XLWorkbook workbook, string fileName)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }
}
