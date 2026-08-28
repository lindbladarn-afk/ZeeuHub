using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the shared fixed-template import flow independently of individual workbook templates.
public sealed class ExcelImportFixedTemplateEngineTests
{
    [Fact]
    public async Task ImportAsync_StagesAllRows_WhenEveryRowIsValid()
    {
        var reader = new StubWorkbookReader(CreateWorkbook(
            (2, "first", "value"),
            (3, "second", "  ")));
        var engine = CreateEngine(reader);
        IReadOnlyCollection<TestStagingRow>? stagedRows = null;

        var result = await engine.ImportAsync(
            CreateFile(),
            "test-user",
            CreateTemplate(),
            (rows, _) =>
            {
                stagedRows = rows;
                return Task.CompletedTask;
            });

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Equal(2, result.StagedRows);
        Assert.NotNull(stagedRows);
        Assert.Collection(
            stagedRows!,
            row =>
            {
                Assert.Equal(2, row.RowNo);
                Assert.Equal("first", row.Value);
                Assert.Equal(2, row.NonEmptyData.Count);
            },
            row =>
            {
                Assert.Equal(3, row.RowNo);
                Assert.Equal("second", row.Value);
                Assert.Single(row.NonEmptyData);
            });
    }

    [Fact]
    public async Task ImportAsync_StagesNothing_WhenAnyRowIsInvalid()
    {
        var reader = new StubWorkbookReader(CreateWorkbook(
            (2, "valid", "value"),
            (3, "invalid", "value")));
        var engine = CreateEngine(reader);
        var stageWasCalled = false;

        var result = await engine.ImportAsync(
            CreateFile(),
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
        Assert.Contains(result.Errors, error => error.Contains("Rad 3: Ogiltigt värde.", StringComparison.Ordinal));
        var invalidRow = Assert.Single(result.RowResults, row => !row.IsValid);
        Assert.Equal("Ogiltigt värde.", invalidRow.ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_ReturnsNoDataError_WhenWorkbookHasOnlyHeaders()
    {
        var engine = CreateEngine(new StubWorkbookReader(CreateWorkbook()));
        var stageWasCalled = false;

        var result = await engine.ImportAsync(
            CreateFile(),
            "test-user",
            CreateTemplate(),
            (_, _) =>
            {
                stageWasCalled = true;
                return Task.CompletedTask;
            });

        Assert.False(stageWasCalled);
        Assert.Equal(0, result.StagedRows);
        Assert.Contains("Testfilen innehåller inga datarader.", result.Errors);
    }

    [Fact]
    public async Task ImportAsync_PropagatesCancellation_BeforeReadingWorkbook()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new CancellationAwareWorkbookReader();
        var engine = CreateEngine(reader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ImportAsync(
            CreateFile(),
            "test-user",
            CreateTemplate(),
            (_, _) => Task.CompletedTask,
            cancellation.Token));
    }

    private static ExcelImportFixedTemplateEngine CreateEngine(IExcelImportWorkbookReader reader)
        => new(
            new StubContextService(),
            reader,
            new ExcelImportResultFactory());

    private static ExcelImportFixedTemplate<TestStagingRow> CreateTemplate(
        Func<string, IEnumerable<string>>? validate = null)
        => new()
        {
            ImportType = "test",
            WorkbookDefinition = new ExcelImportWorkbookDefinition
            {
                ExpectedHeaders = ["Value", "Optional"],
                ValidateHeaders = (_, _) => true,
                BuildRowData = (_, _) => new Dictionary<string, string>(),
                HasAnyValue = _ => true
            },
            ValidateRow = row => validate?.Invoke(row["Value"]) ?? [],
            BuildRowSnapshot = row => row["Value"],
            CreateStagingRow = context => new TestStagingRow(
                context.RowNo,
                context.RowData["Value"],
                context.NonEmptyRowData),
            NoDataError = "Testfilen innehåller inga datarader."
        };

    private static ExcelImportWorkbookReadResult CreateWorkbook(
        params (int RowNo, string Value, string Optional)[] rows)
        => new()
        {
            RowHeaders = ["Value", "Optional"],
            Rows = rows.Select(row => new ExcelImportWorkbookRow
            {
                RowNo = row.RowNo,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = row.Value,
                    ["Optional"] = row.Optional
                }
            }).ToList()
        };

    private static IFormFile CreateFile()
    {
        var stream = new MemoryStream([1]);
        return new FormFile(stream, 0, stream.Length, "file", "test.xlsx");
    }

    private sealed record TestStagingRow(
        int RowNo,
        string Value,
        IReadOnlyDictionary<string, string> NonEmptyData);

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

    private sealed class CancellationAwareWorkbookReader : IExcelImportWorkbookReader
    {
        public Task<ExcelImportWorkbookReadResult> ReadAsync(
            IFormFile file,
            ExcelImportWorkbookDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateWorkbook());
        }
    }
}
