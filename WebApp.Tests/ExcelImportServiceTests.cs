using Microsoft.AspNetCore.Http;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the Excel Import entry point keeps orchestration out of controllers and background jobs.
public sealed class ExcelImportServiceTests
{
    [Fact]
    public async Task RunAsync_Uses_EditSession_Adapter_When_One_Is_Registered()
    {
        var handler = new TestImportHandler("priceupdate");
        var adapter = new TestEditSessionAdapter("priceupdate");
        var service = CreateService([handler], [adapter]);

        var result = await service.RunAsync(CreateFile(), new ExcelImportRunRequest
        {
            ImportType = "PriceUpdate",
            ImportedBy = "tester"
        });

        Assert.Equal("adapter-auto", result.ImportType);
        Assert.Equal(1, adapter.CreateFromFileCalls);
        Assert.Equal(1, adapter.AutoImportCalls);
        Assert.Equal(0, handler.ImportCalls);
    }

    [Fact]
    public async Task RunAsync_Falls_Back_To_Handler_When_No_EditSession_Adapter_Exists()
    {
        var handler = new TestImportHandler("budget");
        var service = CreateService([handler], []);

        var result = await service.RunAsync(CreateFile(), new ExcelImportRunRequest
        {
            ImportType = "BUDGET",
            ImportedBy = "tester"
        });

        Assert.Equal("budget", result.ImportType);
        Assert.Equal(1, handler.ImportCalls);
    }

    [Fact]
    public void IsEditSessionSupported_Reflects_Registered_Adapters()
    {
        var service = CreateService([], [new TestEditSessionAdapter("voucher")]);

        Assert.True(service.IsEditSessionSupported("voucher"));
        Assert.False(service.IsEditSessionSupported("budget"));
    }

    [Fact]
    public async Task RunAsync_Rejects_Voucher_Without_Posting_Date()
    {
        var adapter = new TestEditSessionAdapter("voucher");
        var service = CreateService([], [adapter]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(CreateFile(), new ExcelImportRunRequest
            {
                ImportType = "voucher",
                ImportedBy = "tester"
            }));

        Assert.Contains("Bokföringsdatum", ex.Message);
        Assert.Equal(0, adapter.CreateFromFileCalls);
    }

    private static ExcelImportService CreateService(
        IEnumerable<IExcelImportHandler> handlers,
        IEnumerable<IExcelImportEditSessionAdapter> adapters)
    {
        return new ExcelImportService(
            handlers,
            new ExcelImportEditSessionAdapterResolver(adapters),
            new TestBackgroundJobScheduler());
    }

    private static IFormFile CreateFile()
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", "import.xlsx");
    }

    private sealed class TestImportHandler : IExcelImportHandler
    {
        public TestImportHandler(string importType)
        {
            ImportType = importType;
        }

        public string ImportType { get; }
        public string DisplayName => ImportType;
        public int ImportCalls { get; private set; }

        public bool CanHandle(string? importType)
            => string.Equals(ImportType, importType, StringComparison.OrdinalIgnoreCase);

        public Task<ExcelImportResult> ImportAsync(
            IFormFile file,
            string importedBy,
            CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(ExcelImportResult.Empty(ImportType));
        }
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
        public int CreateFromFileCalls { get; private set; }
        public int AutoImportCalls { get; private set; }

        public Task<ExcelImportResult> CreateEditSessionFromFileAsync(
            IFormFile file,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
        {
            CreateFromFileCalls++;
            return Task.FromResult(ExcelImportResult.Empty("adapter"));
        }

        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty("adapter-empty"));

        public Task<ExcelImportResult> ImportEditedRowsAsync(
            Guid editSessionId,
            string rowsJson,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty("adapter-edited"));

        public Task<ExcelImportResult> TryAutoImportEditedRowsAsync(
            ExcelImportResult result,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
        {
            AutoImportCalls++;
            return Task.FromResult(ExcelImportResult.Empty("adapter-auto"));
        }
    }

    private sealed class TestBackgroundJobScheduler : IExcelImportBackgroundJobScheduler
    {
        public Task<BackgroundJobSnapshot> EnqueueAsync(
            IFormFile file,
            ExcelImportBackgroundJobPayload payload,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
