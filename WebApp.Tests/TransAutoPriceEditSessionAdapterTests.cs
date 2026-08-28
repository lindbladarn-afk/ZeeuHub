using Microsoft.AspNetCore.Http;
using WebApp.Models.SupplierPrice;
using WebApp.Repositories.TransAutoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.TransAutoPrice;

namespace WebApp.Tests;

// Verifies edited Trans Auto price rows reuse the shared Excel Import edit-session adapter flow.
public sealed class TransAutoPriceEditSessionAdapterTests
{
    [Fact]
    public async Task ImportEditedRowsAsync_Stages_Modified_Valid_Rows()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var rowsJson = """
            [
              {
                "rowNo": 12,
                "data": {
                  "Supplier": "OH",
                  "SupplierArticleNo": "00-11-89-1",
                  "CustomerArticleNo": "",
                  "Description": "CLAMP - BOOT",
                  "CurrencyCode": "EUR",
                  "ListPrice": "1.89",
                  "NetPrice": "1.50",
                  "DiscountPercent": "20.5",
                  "Uom": "KG",
                  "MinimumOrderQuantity": "2",
                  "PackageQuantity": "4",
                  "WeightKg": "0.75",
                  "CountryOfOrigin": "SE",
                  "TariffCode": "848390",
                  "ValidFrom": "2026-05-01",
                  "ValidTo": "2027-05-01",
                  "Category1": "Marine",
                  "Category2": "Clamp",
                  "Category3": "Stocked",
                  "Category4": "Priority",
                  "Category5": "2026",
                  "SourceSheetName": "OH Price List",
                  "SourceRowNo": "12"
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            Guid.NewGuid(),
            rowsJson,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Equal("transautoprice", result.ImportType);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Single(repository.Rows);
        Assert.Equal("OH", repository.Rows[0].Supplier);
        Assert.Equal("00-11-89-1", repository.Rows[0].SupplierArticleNo);
        Assert.Equal(1.89m, repository.Rows[0].ListPrice);
        Assert.Equal(1.50m, repository.Rows[0].NetPrice);
        Assert.Equal(20.5m, repository.Rows[0].DiscountPercent);
        Assert.Equal(2m, repository.Rows[0].MinimumOrderQuantity);
        Assert.Equal(4m, repository.Rows[0].PackageQuantity);
        Assert.Equal(0.75m, repository.Rows[0].WeightKg);
        Assert.Equal("SE", repository.Rows[0].CountryOfOrigin);
        Assert.Equal("848390", repository.Rows[0].TariffCode);
        Assert.Equal("Marine", repository.Rows[0].Category1);
        Assert.Equal("Clamp", repository.Rows[0].Category2);
        Assert.Equal("Stocked", repository.Rows[0].Category3);
        Assert.Equal("Priority", repository.Rows[0].Category4);
        Assert.Equal("2026", repository.Rows[0].Category5);
        Assert.Equal(12, repository.Rows[0].SourceRowNo);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_Returns_RowErrors_For_Invalid_Rows()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var rowsJson = """
            [
              {
                "rowNo": 7,
                "data": {
                  "Supplier": "OH",
                  "SupplierArticleNo": "",
                  "CurrencyCode": "EURO",
                  "ListPrice": "not-a-number"
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            Guid.NewGuid(),
            rowsJson,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Empty(repository.Rows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Contains(result.Errors, error => error.Contains("SupplierArticleNo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("CurrencyCode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ListPrice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportEditedRowsAsync_Stops_Before_Staging_When_Any_Row_Is_Invalid()
    {
        var repository = new CapturingTransAutoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var rowsJson = """
            [
              {
                "rowNo": 12,
                "data": {
                  "Supplier": "OH",
                  "SupplierArticleNo": "00-11-89-1",
                  "Description": "CLAMP - BOOT",
                  "CurrencyCode": "EUR",
                  "ListPrice": "1.89",
                  "SourceRowNo": "12"
                }
              },
              {
                "rowNo": 13,
                "data": {
                  "Supplier": "OH",
                  "SupplierArticleNo": "",
                  "Description": "BROKEN PRICE ROW",
                  "CurrencyCode": "EUR",
                  "ListPrice": "not-a-number",
                  "SourceRowNo": "13"
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            Guid.NewGuid(),
            rowsJson,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Empty(repository.Rows);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.NotNull(result.EditSessionId);
        Assert.Contains(result.Errors, error => error.Contains("Inga rader skrevs till stagingtabellen", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.RowResults, row => row.IsValid);
        Assert.Contains(result.RowResults, row => !row.IsValid);
    }

    [Fact]
    public async Task CreateEmptyEditSessionAsync_Returns_Failure_When_Staging_Is_Missing()
    {
        var repository = new MissingTableTransAutoPriceStagingRepository();
        var adapter = CreateAdapter(repository);

        var result = await adapter.CreateEmptyEditSessionAsync("tester", new ExcelImportEditSessionContext());

        Assert.Equal(0, result.ValidRows);
        Assert.Contains(result.Errors, error => error.Contains("stagingtabellen", StringComparison.OrdinalIgnoreCase));
    }

    private static TransAutoPriceEditSessionAdapter CreateAdapter(CapturingTransAutoPriceStagingRepository repository)
        => new(
            new ThrowingTransAutoPriceImportService(),
            repository,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory());

    private static TransAutoPriceEditSessionAdapter CreateAdapter(MissingTableTransAutoPriceStagingRepository repository)
        => new(
            new ThrowingTransAutoPriceImportService(),
            repository,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory());

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

    private sealed class MissingTableTransAutoPriceStagingRepository : ITransAutoPriceStagingRepository
    {
        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task BulkInsertAsync(IEnumerable<PortalSupplierPriceStagingRow> rows, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

    private sealed class ThrowingTransAutoPriceImportService : ITransAutoPriceImportService
    {
        public Task<ExcelImportResult> ImportAsync(
            IFormFile file,
            string importedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
