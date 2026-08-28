using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies validation, limits, normalization, and staging in the shared edit-session engine.
public sealed class ExcelImportEditSessionEngineTests
{
    [Fact]
    public async Task CreateFromFileAsync_RejectsRowsBeyondEditableLimit()
    {
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook("one", "two")));

        var exception = await Assert.ThrowsAsync<EditableImportRowLimitExceededException>(() =>
            engine.CreateFromFileAsync(CreateFile(), CreateTemplate(), maxRows: 1));

        Assert.Equal(2, exception.RowCount);
        Assert.Equal(1, exception.MaxRows);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_StagesNothing_WhenAnyRowIsInvalid()
    {
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook()));
        var stageWasCalled = false;

        var result = await engine.ImportEditedRowsAsync(
            Guid.NewGuid(),
            [CreateEditableRow(4, "valid"), CreateEditableRow(5, "invalid")],
            "test-user",
            CreateTemplate(value => value == "invalid" ? ["Ogiltigt värde."] : []),
            (_, _) =>
            {
                stageWasCalled = true;
                return Task.CompletedTask;
            });

        Assert.False(stageWasCalled);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(0, result.StagedRows);
        Assert.Contains(result.Errors, error => error.Contains("Rad 5: Ogiltigt värde.", StringComparison.Ordinal));
        var invalidRow = Assert.Single(result.RowResults, row => !row.IsValid);
        Assert.Equal("Ogiltigt värde.", invalidRow.ErrorMessage);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_NormalizesAndStagesAllValidRows()
    {
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook()));
        IReadOnlyCollection<TestStagingRow>? stagedRows = null;

        var result = await engine.ImportEditedRowsAsync(
            Guid.NewGuid(),
            [CreateEditableRow(0, "  valid  ")],
            "test-user",
            CreateTemplate(),
            (rows, _) =>
            {
                stagedRows = rows;
                return Task.CompletedTask;
            });

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.StagedRows);
        Assert.Null(result.EditSessionId);
        var staged = Assert.Single(stagedRows!);
        Assert.Equal(1, staged.RowNo);
        Assert.Equal("valid", staged.Value);
        Assert.Equal("test-user", staged.ImportedBy);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_PropagatesCancellation_DuringValidation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ImportEditedRowsAsync(
            Guid.NewGuid(),
            [CreateEditableRow(1, "valid")],
            "test-user",
            CreateTemplate(),
            (_, _) => Task.CompletedTask,
            cancellation.Token));
    }

    [Fact]
    public async Task ImportEditedRowsAsync_RejectsOversizedCellWithoutEchoingItsContents()
    {
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook()));
        var oversizedValue = new string('S', ExcelImportResourceLimits.MaxCellLength + 1);

        var result = await engine.ImportEditedRowsAsync(
            Guid.NewGuid(),
            [CreateEditableRow(7, oversizedValue)],
            "test-user",
            CreateTemplate(),
            (_, _) => Task.CompletedTask);

        Assert.Equal(1, result.InvalidRows);
        var error = Assert.Single(result.Errors);
        Assert.Contains("cell är för lång", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(oversizedValue, error, StringComparison.Ordinal);
    }

    private static ExcelImportEditSessionEngine CreateEngine(IExcelImportWorkbookReader reader)
        => new(new StubContextService(), reader, new ExcelImportResultFactory());

    private static ExcelImportEditTemplate<TestStagingRow> CreateTemplate(
        Func<string, IEnumerable<string>>? validate = null)
        => new()
        {
            ImportType = "test",
            Headers = ["Value"],
            WorkbookDefinition = new ExcelImportWorkbookDefinition
            {
                ExpectedHeaders = ["Value"],
                ValidateHeaders = (_, _) => true,
                BuildRowData = (_, _) => new Dictionary<string, string>(),
                HasAnyValue = _ => true
            },
            ValidateRow = row => validate?.Invoke(row["Value"]) ?? [],
            BuildRowSnapshot = row => row["Value"],
            HasAnyValue = row => !string.IsNullOrWhiteSpace(row["Value"]),
            NormalizeValue = (_, value) => value.Trim(),
            CreateStagingRow = context => new TestStagingRow(
                context.RowNo,
                context.RowData["Value"],
                context.ImportedBy)
        };

    private static ExcelImportEditableRow CreateEditableRow(int rowNo, string value)
        => new()
        {
            RowNo = rowNo,
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Value"] = value
            }
        };

    private static ExcelImportWorkbookReadResult CreateWorkbook(params string[] values)
        => new()
        {
            RowHeaders = ["Value"],
            Rows = values.Select((value, index) => new ExcelImportWorkbookRow
            {
                RowNo = index + 2,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = value
                }
            }).ToList()
        };

    private static IFormFile CreateFile()
    {
        var stream = new MemoryStream([1]);
        return new FormFile(stream, 0, stream.Length, "file", "test.xlsx");
    }

    private sealed record TestStagingRow(int RowNo, string Value, string ImportedBy);

    private sealed class StubContextService : IExcelImportContextService
    {
        public ExcelImportUserContext GetCurrent()
            => new()
            {
                CompanyId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                ForetagKod = 100,
                UserId = "test-user"
            };
    }

    private sealed class StubWorkbookReader(ExcelImportWorkbookReadResult result) : IExcelImportWorkbookReader
    {
        public Task<ExcelImportWorkbookReadResult> ReadAsync(
            IFormFile file,
            ExcelImportWorkbookDefinition definition,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
