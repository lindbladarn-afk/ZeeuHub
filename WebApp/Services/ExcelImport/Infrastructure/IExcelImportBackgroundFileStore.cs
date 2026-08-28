namespace WebApp.Services.ExcelImport;

// Stores uploaded import files until the background worker has consumed them.
public interface IExcelImportBackgroundFileStore
{
    Task<StoredExcelImportFile> SaveAsync(IFormFile file, Guid companyId, CancellationToken cancellationToken);
    void DeleteQuietly(string? path);
    int CleanupExpired(
        DateTime cutoffUtc,
        IReadOnlySet<string> protectedPaths,
        CancellationToken cancellationToken);
}

public sealed class StoredExcelImportFile
{
    public string Path { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}
