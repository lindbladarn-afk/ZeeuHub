using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.SupplierPrice;
using WebApp.Repositories.TransAutoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.SupplierPrice;
using WebApp.Services.TransAutoPrice;

namespace WebApp.Tests;

// Verifies the Trans Auto import short-circuits before workbook parsing when staging is unavailable.
public sealed class TransAutoPriceImportServiceTests
{
    [Fact]
    public async Task ImportAsync_Returns_Fast_Failure_When_Staging_Table_Is_Missing()
    {
        var repository = new MissingTableTransAutoPriceStagingRepository();
        var service = new TransAutoPriceImportService(
            repository,
            new CapturingExcelImportRowResultStore(),
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<TransAutoPriceImportService>());

        var result = await service.ImportAsync(CreateFile(), "tester");

        Assert.Equal("transautoprice", result.ImportType);
        Assert.Contains(result.Errors, error => error.Contains("stagingtabellen", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Rows);
    }

    [Fact]
    public async Task ImportAsync_Stages_Valid_Oh_File_Using_Streaming_Reader()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var logger = new CapturingLogger<TransAutoPriceImportService>();
        var service = new TransAutoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            logger);

        await using var stream = BuildOhWorkbook();
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal("transautoprice", result.ImportType);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Single(repository.Rows);
        Assert.Equal("OH", repository.Rows[0].Supplier);
        Assert.Equal("00-11-89-1", repository.Rows[0].SupplierArticleNo);
        Assert.Equal(1.89m, repository.Rows[0].ListPrice);
        Assert.Equal("EUR", repository.Rows[0].CurrencyCode);
        Assert.Single(rowResultStore.Rows);
        Assert.True(rowResultStore.Rows[0].IsValid);
        Assert.Equal(SupplierPriceImportColumns.ResultHeaders, result.RowHeaders);
        var resultRow = Assert.Single(result.RowResults);
        Assert.Equal("0.75", resultRow.Data["WeightKg"]);
        Assert.Equal("SE", resultRow.Data["CountryOfOrigin"]);
        Assert.Equal("Marine", resultRow.Data["Category1"]);
        Assert.Equal("5", resultRow.Data["Category2"]);
        Assert.Equal("2026-05-01", resultRow.Data["Category5"]);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Properties.ContainsKey("FileExtension"));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("OH Price List.xlsx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_Returns_Safe_Failure_When_Staging_Insert_Fails()
    {
        var repository = new ThrowingTransAutoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var logger = new CapturingLogger<TransAutoPriceImportService>();
        var service = new TransAutoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            logger);

        await using var stream = BuildOhWorkbook();
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal("transautoprice", result.ImportType);
        Assert.Contains(result.Errors, error => error.Contains("TRANS_AUTO_PRICE_STAGING_FAILED", StringComparison.OrdinalIgnoreCase));
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal("TRANS_AUTO_PRICE_STAGING_FAILED", error.Properties["ErrorCode"]?.ToString());
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.DoesNotContain("secret-value", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OH Price List.xlsx", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_Stops_Before_Staging_When_Any_Row_Is_Invalid()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var service = new TransAutoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<TransAutoPriceImportService>());

        await using var stream = BuildOhWorkbook(includeInvalidRow: true);
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal("transautoprice", result.ImportType);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Contains(result.Errors, error => error.Contains("Inga rader skrevs till stagingtabellen", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Rows);
        Assert.Equal(2, rowResultStore.Rows.Count);
        Assert.Contains(rowResultStore.Rows, row => row.IsValid);
        var invalidStoredRow = Assert.Single(rowResultStore.Rows, row => !row.IsValid);
        Assert.Contains("artikelnummer saknas", invalidStoredRow.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("listpris saknas", invalidStoredRow.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var invalidPreviewRow = Assert.Single(result.RowResults, row => !row.IsValid);
        Assert.Equal(invalidStoredRow.ErrorMessage, invalidPreviewRow.ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_Ignores_Halyard_Section_Labels()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var service = new TransAutoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<TransAutoPriceImportService>());

        await using var stream = BuildHalyardWorkbook();
        var file = new FormFile(stream, 0, stream.Length, "file", "Halyard Euro Retail Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Equal(1, result.StagedRows);
        Assert.Single(repository.Rows);
        Assert.Equal("H-100", repository.Rows[0].SupplierArticleNo);
        Assert.Single(rowResultStore.Rows);
    }

    [Fact]
    public async Task ImportAsync_Ignores_Oh_Legal_Footer()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var service = new TransAutoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<TransAutoPriceImportService>());

        await using var stream = BuildOhWorkbook(includeFooter: true);
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Equal(1, result.StagedRows);
        Assert.Single(repository.Rows);
        Assert.Single(rowResultStore.Rows);
    }

    [Fact]
    public async Task ImportAsync_Rejects_Workbook_With_Too_Many_Columns()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var service = CreateService(repository);
        await using var stream = BuildOhWorkbook(addColumnBeyondLimit: true);
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Contains(result.Errors, error => error.Contains("kolumner", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Rows);
    }

    [Fact]
    public async Task ImportAsync_Rejects_Workbook_With_Oversized_Cell()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var service = CreateService(repository);
        await using var stream = BuildOhWorkbook(oversizedDescription: true);
        var file = new FormFile(stream, 0, stream.Length, "file", "OH Price List.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Contains(result.Errors, error => error.Contains("cell är för lång", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Rows);
    }

    private static IFormFile CreateFile()
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", "price_list_26.xlsx");
    }

    private static TransAutoPriceImportService CreateService(ITransAutoPriceStagingRepository repository)
        => new(
            repository,
            new CapturingExcelImportRowResultStore(),
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<TransAutoPriceImportService>());

    private static MemoryStream BuildOhWorkbook(
        bool includeInvalidRow = false,
        bool includeFooter = false,
        bool addColumnBeyondLimit = false,
        bool oversizedDescription = false)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("OH Price List");

        worksheet.Cell(10, 1).Value = "OH Part Number";
        worksheet.Cell(10, 2).Value = "Customer Part Number";
        worksheet.Cell(10, 3).Value = "OH Part Description";
        worksheet.Cell(10, 4).Value = "Currency";
        worksheet.Cell(10, 5).Value = "Standard Price";
        worksheet.Cell(10, 6).Value = "Special Price";
        worksheet.Cell(10, 7).Value = "UOM";
        worksheet.Cell(10, 8).Value = "Minimum Order Quantity";
        worksheet.Cell(10, 9).Value = "Packaging Quantity";
        worksheet.Cell(10, 10).Value = "Unit Weight Gross";
        worksheet.Cell(10, 11).Value = "Country of Origin";
        worksheet.Cell(10, 12).Value = "Product Line";
        worksheet.Cell(10, 13).Value = "Published Lead Time Days";
        worksheet.Cell(10, 14).Value = "Unit Core Deposit";
        worksheet.Cell(10, 15).Value = "Emergency Price";
        worksheet.Cell(10, 16).Value = "Part Price Was Last Updated";
        worksheet.Cell(11, 1).Value = "00-11-89-1";
        worksheet.Cell(11, 2).Value = "C-123";
        worksheet.Cell(11, 3).Value = oversizedDescription
            ? new string('A', ExcelImportResourceLimits.MaxCellLength + 1)
            : "CLAMP - BOOT";
        worksheet.Cell(11, 4).Value = "EUR";
        worksheet.Cell(11, 5).Value = 1.89m;
        worksheet.Cell(11, 6).Value = 1.50m;
        worksheet.Cell(11, 7).Value = "KG";
        worksheet.Cell(11, 8).Value = 2m;
        worksheet.Cell(11, 9).Value = 4m;
        worksheet.Cell(11, 10).Value = 0.75m;
        worksheet.Cell(11, 11).Value = "SE";
        worksheet.Cell(11, 12).Value = "Marine";
        worksheet.Cell(11, 13).Value = 5m;
        worksheet.Cell(11, 14).Value = 10m;
        worksheet.Cell(11, 15).Value = 2.25m;
        worksheet.Cell(11, 16).Value = new DateTime(2026, 5, 1);

        if (addColumnBeyondLimit)
            worksheet.Cell(10, ExcelImportResourceLimits.MaxColumns + 1).Value = "Unexpected";

        if (includeInvalidRow)
        {
            worksheet.Cell(12, 1).Value = "";
            worksheet.Cell(12, 2).Value = "C-456";
            worksheet.Cell(12, 3).Value = "BROKEN PRICE ROW";
            worksheet.Cell(12, 4).Value = "EUR";
            worksheet.Cell(12, 5).Value = "not-a-price";
            worksheet.Cell(12, 7).Value = "KG";
        }

        if (includeFooter)
        {
            var footerRow = includeInvalidRow ? 13 : 12;
            worksheet.Cell(footerRow, 1).Value = "This price list is confidential and may not be distributed without prior written approval.";
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildHalyardWorkbook()
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Halyard");

        worksheet.Cell(5, 1).Value = "Part No";
        worksheet.Cell(5, 2).Value = "Reference";
        worksheet.Cell(5, 3).Value = "Description";
        worksheet.Cell(5, 4).Value = "Unit Price Euro";
        worksheet.Cell(5, 5).Value = "UOM";
        worksheet.Cell(5, 6).Value = "Catalogue Page No";
        worksheet.Cell(7, 1).Value = "Moulded Lift Silencers Top In Top Out";
        worksheet.Cell(8, 1).Value = "H-100";
        worksheet.Cell(8, 2).Value = "REF-100";
        worksheet.Cell(8, 3).Value = "Lift silencer";
        worksheet.Cell(8, 4).Value = 125.50m;
        worksheet.Cell(8, 5).Value = "EA";
        worksheet.Cell(8, 6).Value = "12";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private sealed class MissingTableTransAutoPriceStagingRepository : ITransAutoPriceStagingRepository
    {
        public List<PortalSupplierPriceStagingRow> Rows { get; } = new();

        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task BulkInsertAsync(IEnumerable<PortalSupplierPriceStagingRow> rows, CancellationToken cancellationToken = default)
        {
            Rows.AddRange(rows);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingTransAutoPriceStagingRepository : ITransAutoPriceStagingRepository
    {
        public List<PortalSupplierPriceStagingRow> Rows { get; } = new();

        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(IEnumerable<PortalSupplierPriceStagingRow> rows, CancellationToken cancellationToken = default)
        {
            Rows.AddRange(rows);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTransAutoPriceStagingRepository : ITransAutoPriceStagingRepository
    {
        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(IEnumerable<PortalSupplierPriceStagingRow> rows, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private sealed class StaticExcelImportContextService : IExcelImportContextService
    {
        public ExcelImportUserContext GetCurrent() => new()
        {
            CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ForetagKod = 9900,
            UserId = "user-1"
        };
    }

    private sealed class CapturingExcelImportRowResultStore : IExcelImportRowResultStore
    {
        public List<ExcelImportStoredRowResult> Rows { get; } = new();

        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(IEnumerable<ExcelImportStoredRowResult> rows, CancellationToken cancellationToken = default)
        {
            Rows.AddRange(rows);
            return Task.CompletedTask;
        }

        public Task CleanupOldRowsAsync(int retentionDays = JeevesExcelImportRowResultStore.DefaultRetentionDays, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ExcelImportStoredRowPage> GetPageAsync(
            Guid companyId,
            string importType,
            Guid importBatchId,
            int page,
            int pageSize,
            bool showOnlyInvalidRows,
            bool showAllRows = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportStoredRowPage());
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
