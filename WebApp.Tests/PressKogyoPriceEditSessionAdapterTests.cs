using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Models.SupplierPrice;
using WebApp.Repositories.PressKogyoPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.PressKogyoPrice;
using WebApp.Services.SupplierPrice;

namespace WebApp.Tests;

// Verifies Press Kogyo rows use the shared supplier-price edit flow safely.
public sealed class PressKogyoPriceEditSessionAdapterTests
{
    [Fact]
    public async Task ImportEditedRowsAsync_Stages_All_Normalized_Fields()
    {
        var repository = new CapturingPressKogyoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var rowsJson = """
            [
              {
                "rowNo": 8,
                "data": {
                  "Supplier": "Press Kogyo",
                  "SupplierArticleNo": "PK-100",
                  "CustomerArticleNo": "AL-200",
                  "Description": "Bracket",
                  "CurrencyCode": "SEK",
                  "ListPrice": "123,45",
                  "NetPrice": "120.00",
                  "DiscountPercent": "2.5",
                  "Uom": "ST",
                  "MinimumOrderQuantity": "10",
                  "PackageQuantity": "20",
                  "WeightKg": "0,75",
                  "CountryOfOrigin": "SE",
                  "TariffCode": "870899",
                  "ValidFrom": "2026-01-01",
                  "ValidTo": "2026-06-30",
                  "Category1": "Gross weight",
                  "Category2": "Scrap",
                  "Category3": "Adjustment SEK",
                  "Category4": "Remaining increase",
                  "Category5": "2026",
                  "SourceSheetName": "Price List",
                  "SourceRowNo": "8"
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            Guid.NewGuid(),
            rowsJson,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Equal("presskogyoprice", result.ImportType);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Equal(1, result.StagedRows);
        Assert.Null(result.EditSessionId);

        var row = Assert.Single(repository.Rows);
        Assert.Equal("Press Kogyo", row.Supplier);
        Assert.Equal("PK-100", row.SupplierArticleNo);
        Assert.Equal("AL-200", row.CustomerArticleNo);
        Assert.Equal("Bracket", row.Description);
        Assert.Equal("SEK", row.CurrencyCode);
        Assert.Equal(123.45m, row.ListPrice);
        Assert.Equal(120m, row.NetPrice);
        Assert.Equal(2.5m, row.DiscountPercent);
        Assert.Equal("ST", row.Uom);
        Assert.Equal(10m, row.MinimumOrderQuantity);
        Assert.Equal(20m, row.PackageQuantity);
        Assert.Equal(0.75m, row.WeightKg);
        Assert.Equal("SE", row.CountryOfOrigin);
        Assert.Equal("870899", row.TariffCode);
        Assert.Equal(new DateTime(2026, 1, 1), row.ValidFrom);
        Assert.Equal(new DateTime(2026, 6, 30), row.ValidTo);
        Assert.Equal("Gross weight", row.Category1);
        Assert.Equal("Scrap", row.Category2);
        Assert.Equal("Adjustment SEK", row.Category3);
        Assert.Equal("Remaining increase", row.Category4);
        Assert.Equal("2026", row.Category5);
        Assert.Equal("Price List", row.SourceSheetName);
        Assert.Equal(8, row.SourceRowNo);
        Assert.Equal("tester", row.ImportedBy);
        Assert.Equal(CompanyId, row.CompanyId);
        Assert.Equal(9900, row.ForetagKod);
        Assert.Equal("user-1", row.UserId);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_Stops_Entire_Batch_When_One_Row_Is_Invalid()
    {
        var repository = new CapturingPressKogyoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var editSessionId = Guid.NewGuid();
        var rowsJson = """
            [
              {
                "rowNo": 8,
                "data": {
                  "Supplier": "Press Kogyo",
                  "SupplierArticleNo": "PK-100",
                  "CustomerArticleNo": "AL-200",
                  "CurrencyCode": "SEK",
                  "ListPrice": "123.45"
                }
              },
              {
                "rowNo": 9,
                "data": {
                  "Supplier": "Press Kogyo",
                  "SupplierArticleNo": "PK-101",
                  "CustomerArticleNo": "",
                  "CurrencyCode": "SEKK",
                  "ListPrice": "invalid",
                  "ValidFrom": "not-a-date",
                  "SourceRowNo": "8.5"
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            editSessionId,
            rowsJson,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Empty(repository.Rows);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(0, result.StagedRows);
        Assert.Equal(editSessionId, result.EditSessionId);
        Assert.Contains(result.Errors, error => error.Contains("Inga rader skrevs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("CustomerArticleNo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("CurrencyCode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ListPrice", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("ValidFrom", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("SourceRowNo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportEditedRowsAsync_Rejects_Negative_Prices()
    {
        var repository = new CapturingPressKogyoPriceStagingRepository();
        var adapter = CreateAdapter(repository);
        var rowsJson = """
            [
              {
                "rowNo": 8,
                "data": {
                  "Supplier": "Press Kogyo",
                  "SupplierArticleNo": "PK-100",
                  "CustomerArticleNo": "AL-200",
                  "CurrencyCode": "SEK",
                  "ListPrice": "-1",
                  "NetPrice": "-0,50"
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
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(0, result.StagedRows);
        Assert.Contains(result.Errors, error => error.Contains("ListPrice får inte vara negativt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("NetPrice får inte vara negativt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateEmptyEditSessionAsync_Returns_All_Shared_Columns()
    {
        var adapter = CreateAdapter(new CapturingPressKogyoPriceStagingRepository());

        var result = await adapter.CreateEmptyEditSessionAsync(
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Equal("presskogyoprice", result.ImportType);
        Assert.NotNull(result.EditSessionId);
        Assert.Equal(SupplierPriceImportColumns.ResultHeaders, result.RowHeaders);
        var row = Assert.Single(result.RowResults);
        Assert.Equal(SupplierPriceImportColumns.ResultHeaders.Count, row.Data.Count);
        Assert.All(SupplierPriceImportColumns.ResultHeaders, header => Assert.True(row.Data.ContainsKey(header)));
    }

    [Fact]
    public async Task CreateEmptyEditSessionAsync_Fails_Safely_When_Staging_Table_Is_Missing()
    {
        var adapter = CreateAdapter(new MissingPressKogyoPriceStagingRepository());

        var result = await adapter.CreateEmptyEditSessionAsync(
            "tester",
            new ExcelImportEditSessionContext());

        Assert.Equal("presskogyoprice", result.ImportType);
        Assert.Equal(0, result.TotalRows);
        Assert.Empty(result.RowResults);
        Assert.Contains(result.Errors, error => error.Contains("Press Kogyo-stagingtabellen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddExcelImportServices_Registers_PressKogyo_Edit_Adapter()
    {
        var services = new ServiceCollection();

        services.AddExcelImportServices();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IExcelImportEditSessionAdapter)
                          && descriptor.ImplementationType == typeof(PressKogyoPriceEditSessionAdapter));
    }

    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static PressKogyoPriceEditSessionAdapter CreateAdapter(
        IPressKogyoPriceStagingRepository repository)
        => new(
            new ThrowingPressKogyoPriceImportService(),
            repository,
            new StaticExcelImportContextService(),
            new ExcelImportResultFactory());

    private sealed class CapturingPressKogyoPriceStagingRepository : IPressKogyoPriceStagingRepository
    {
        public List<PortalSupplierPriceStagingRow> Rows { get; } = new();

        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(
            IEnumerable<PortalSupplierPriceStagingRow> rows,
            CancellationToken cancellationToken = default)
        {
            Rows.AddRange(rows);
            return Task.CompletedTask;
        }
    }

    private sealed class MissingPressKogyoPriceStagingRepository : IPressKogyoPriceStagingRepository
    {
        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task BulkInsertAsync(
            IEnumerable<PortalSupplierPriceStagingRow> rows,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StaticExcelImportContextService : IExcelImportContextService
    {
        public ExcelImportUserContext GetCurrent() => new()
        {
            CompanyId = CompanyId,
            ForetagKod = 9900,
            UserId = "user-1"
        };
    }

    private sealed class ThrowingPressKogyoPriceImportService : IPressKogyoPriceImportService
    {
        public Task<ExcelImportResult> ImportAsync(
            IFormFile file,
            string importedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
