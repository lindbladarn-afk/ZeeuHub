using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies safe storage boundaries for temporary Excel Import job files.
public sealed class LocalExcelImportBackgroundFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"excel-import-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_Stores_File_In_Tenant_Directory_And_Delete_Removes_It()
    {
        var companyId = Guid.NewGuid();
        var store = CreateStore();
        var content = new byte[] { 1, 2, 3 };
        var file = new FormFile(new MemoryStream(content), 0, content.Length, "file", "prices.xlsx");

        var stored = await store.SaveAsync(file, companyId, CancellationToken.None);

        Assert.StartsWith(Path.Combine(_root, "App_Data", "ExcelImportJobs", companyId.ToString("N")), stored.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(stored.Path));
        Assert.Equal(content, await File.ReadAllBytesAsync(stored.Path));

        store.DeleteQuietly(stored.Path);
        Assert.False(File.Exists(stored.Path));
    }

    [Fact]
    public async Task SaveAsync_Rejects_Unsupported_Extension()
    {
        var store = CreateStore();
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "payload.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(file, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteQuietly_Refuses_Path_Outside_Storage_Root()
    {
        Directory.CreateDirectory(_root);
        var outsidePath = Path.Combine(_root, "outside.xlsx");
        await File.WriteAllBytesAsync(outsidePath, new byte[] { 1 });
        var store = CreateStore();

        store.DeleteQuietly(outsidePath);

        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task SaveAsync_UsesConfiguredSharedStorageRoot()
    {
        var sharedRoot = Path.Combine(_root, "shared");
        var store = CreateStore(new ExcelImportBackgroundFileStoreOptions
        {
            StorageRoot = sharedRoot
        });
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "prices.xlsx");

        var stored = await store.SaveAsync(file, Guid.NewGuid(), CancellationToken.None);

        Assert.StartsWith(Path.GetFullPath(sharedRoot), stored.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_RequiresConfiguredStorageInProduction_WithoutBreakingServiceResolution()
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            EnvironmentName = "Production"
        };
        var store = new LocalExcelImportBackgroundFileStore(
            environment,
            Options.Create(new ExcelImportBackgroundFileStoreOptions()),
            NullLogger<LocalExcelImportBackgroundFileStore>.Instance);
        var file = CreateFormFile("prices.xlsx");

        var exception = await Assert.ThrowsAsync<ExcelImportStorageConfigurationException>(() =>
            store.SaveAsync(file, Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("delat beständigt filsystem", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanupExpired_RemovesOnlyOldFilesInsideStorageRoot()
    {
        var store = CreateStore();
        var companyId = Guid.NewGuid();
        var oldFile = await store.SaveAsync(
            CreateFormFile("old.xlsx"),
            companyId,
            CancellationToken.None);
        var freshFile = await store.SaveAsync(
            CreateFormFile("fresh.xlsx"),
            companyId,
            CancellationToken.None);
        File.SetLastWriteTimeUtc(oldFile.Path, DateTime.UtcNow.AddDays(-5));

        var deleted = store.CleanupExpired(
            DateTime.UtcNow.AddDays(-2),
            new HashSet<string>(),
            CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldFile.Path));
        Assert.True(File.Exists(freshFile.Path));
    }

    [Fact]
    public async Task CleanupExpired_PreservesOldFileReferencedByActiveJob()
    {
        var store = CreateStore();
        var storedFile = await store.SaveAsync(
            CreateFormFile("active.xlsx"),
            Guid.NewGuid(),
            CancellationToken.None);
        File.SetLastWriteTimeUtc(storedFile.Path, DateTime.UtcNow.AddDays(-5));

        var deleted = store.CleanupExpired(
            DateTime.UtcNow.AddDays(-2),
            new HashSet<string> { Path.GetFullPath(storedFile.Path) },
            CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(storedFile.Path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private LocalExcelImportBackgroundFileStore CreateStore(ExcelImportBackgroundFileStoreOptions? options = null)
        => new(
            new TestWebHostEnvironment { ContentRootPath = _root },
            Options.Create(options ?? new ExcelImportBackgroundFileStoreOptions()),
            NullLogger<LocalExcelImportBackgroundFileStore>.Instance);

    private static IFormFile CreateFormFile(string fileName)
        => new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", fileName);

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WebApp.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
