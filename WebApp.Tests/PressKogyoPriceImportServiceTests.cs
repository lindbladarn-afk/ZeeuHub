using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.SupplierPrice;
using WebApp.Repositories.PressKogyoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.PressKogyoPrice;

namespace WebApp.Tests;

// Verifies Press Kogyo price-list workbooks are parsed and staged through the supplier price engine.
public sealed class PressKogyoPriceImportServiceTests
{
    [Fact]
    public async Task ImportAsync_Stages_Valid_Press_Kogyo_File()
    {
        var repository = new CapturingPressKogyoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var service = new PressKogyoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<PressKogyoPriceImportService>());

        await using var stream = BuildPressKogyoWorkbook();
        var file = new FormFile(stream, 0, stream.Length, "file", "Press Kogyo prislista.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal("presskogyoprice", result.ImportType);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Equal(2, repository.Rows.Count);
        Assert.All(repository.Rows, row => Assert.Equal("Press Kogyo", row.Supplier));
        Assert.Equal("497010", repository.Rows[0].SupplierArticleNo);
        Assert.Equal("5211600", repository.Rows[0].CustomerArticleNo);
        Assert.Equal(1m, repository.Rows[0].MinimumOrderQuantity);
        Assert.Equal(11250.597975m, repository.Rows[0].ListPrice);
        Assert.Equal("SEK", repository.Rows[0].CurrencyCode);
        Assert.Equal(new DateTime(2026, 1, 1), repository.Rows[0].ValidFrom);
        Assert.Equal(115.935m, repository.Rows[0].WeightKg);
        Assert.Equal("2026-01-01", repository.Rows[0].SourceSheetName);
        Assert.Equal(8, repository.Rows[0].SourceRowNo);
        Assert.Equal(2, rowResultStore.Rows.Count);
        Assert.All(rowResultStore.Rows, row => Assert.True(row.IsValid));
    }

    [Fact]
    public async Task ImportAsync_Stops_Before_Staging_When_Press_Kogyo_Row_Is_Invalid()
    {
        var repository = new CapturingPressKogyoPriceStagingRepository();
        var rowResultStore = new CapturingExcelImportRowResultStore();
        var service = new PressKogyoPriceImportService(
            repository,
            rowResultStore,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory(),
            new CapturingLogger<PressKogyoPriceImportService>());

        await using var stream = BuildPressKogyoWorkbook(includeInvalidRow: true);
        var file = new FormFile(stream, 0, stream.Length, "file", "Press Kogyo prislista.xlsx");

        var result = await service.ImportAsync(file, "tester");

        Assert.Equal("presskogyoprice", result.ImportType);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Contains(result.Errors, error => error.Contains("Inga rader skrevs till stagingtabellen", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Rows);
        Assert.Equal(3, rowResultStore.Rows.Count);
    }

    private static MemoryStream BuildPressKogyoWorkbook(bool includeInvalidRow = false)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("2026-01-01");
        sheet.Cell(2, 3).Value = "Prisändringsdatum";
        sheet.Cell(3, 3).Value = new DateTime(2026, 1, 1);
        sheet.Cell(7, 1).Value = "Art.nr PKS";
        sheet.Cell(7, 2).Value = "Art.nr Ålö";
        sheet.Cell(7, 3).Value = "Artikelbeskrivning";
        sheet.Cell(7, 4).Value = "Antal stafflad prislista";
        sheet.Cell(7, 5).Value = "Bruttovikt";
        sheet.Cell(7, 6).Value = "Skrot";
        sheet.Cell(7, 7).Value = "Nettovikt";
        sheet.Cell(7, 17).Value = "Prisjustering för 2001 - 2508 %";
        sheet.Cell(7, 18).Value = "Prisjustering för 2001 - 2508 SEK";
        sheet.Cell(7, 19).Value = "Nytt pris Januari-Juni 2026";
        sheet.Cell(7, 21).Value = "Återstående prishöjning";

        AddRow(sheet, 8, "497010", "5211600", "Lastarmskit stl. 0", 1m, 149.42m, 33.485m, 115.935m, 0.04m, 219.296725m, 11250.597975m);
        AddRow(sheet, 9, "497010", "5211600", "Lastarmskit stl. 0", 5m, 149.42m, 33.485m, 115.935m, 0.04m, 88.511125m, 4580.532375m);

        if (includeInvalidRow)
            AddRow(sheet, 10, "", "5211600", "Broken", 10m, 149.42m, 33.485m, 115.935m, 0.04m, 72.025125m, 3739.746375m);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void AddRow(
        IXLWorksheet sheet,
        int row,
        string supplierArticleNo,
        string customerArticleNo,
        string description,
        decimal quantityBreak,
        decimal grossWeight,
        decimal scrapWeight,
        decimal netWeight,
        decimal discountPercent,
        decimal adjustment,
        decimal price)
    {
        sheet.Cell(row, 1).Value = supplierArticleNo;
        sheet.Cell(row, 2).Value = customerArticleNo;
        sheet.Cell(row, 3).Value = description;
        sheet.Cell(row, 4).Value = quantityBreak;
        sheet.Cell(row, 5).Value = grossWeight;
        sheet.Cell(row, 6).Value = scrapWeight;
        sheet.Cell(row, 7).Value = netWeight;
        sheet.Cell(row, 17).Value = discountPercent;
        sheet.Cell(row, 18).Value = adjustment;
        sheet.Cell(row, 19).Value = price;
        sheet.Cell(row, 21).Value = adjustment;
    }

    private sealed class CapturingPressKogyoPriceStagingRepository : IPressKogyoPriceStagingRepository
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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
