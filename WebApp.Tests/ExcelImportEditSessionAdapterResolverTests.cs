using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies edit-session adapter lookup by import type.
public sealed class ExcelImportEditSessionAdapterResolverTests
{
    [Fact]
    public void Find_Returns_Adapter_Case_Insensitive()
    {
        var adapter = new TestEditSessionAdapter("voucher");
        var resolver = new ExcelImportEditSessionAdapterResolver(new[] { adapter });

        var resolved = resolver.Find("Voucher");

        Assert.Same(adapter, resolved);
    }

    [Fact]
    public void Find_Returns_Null_For_Unknown_Type()
    {
        var resolver = new ExcelImportEditSessionAdapterResolver(new[] { new TestEditSessionAdapter("voucher") });

        var resolved = resolver.Find("budget");

        Assert.Null(resolved);
    }

    private sealed class TestEditSessionAdapter : IExcelImportEditSessionAdapter
    {
        public TestEditSessionAdapter(string importType)
        {
            ImportType = importType;
        }

        public string ImportType { get; }
        public string EditSessionFileName => "test";
        public int MaxEditableRows => 1000;

        public Task<ExcelImportResult> CreateEditSessionFromFileAsync(
            IFormFile file,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty(ImportType));

        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty(ImportType));

        public Task<ExcelImportResult> ImportEditedRowsAsync(
            Guid editSessionId,
            string rowsJson,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty(ImportType));

        public Task<ExcelImportResult> TryAutoImportEditedRowsAsync(
            ExcelImportResult result,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
