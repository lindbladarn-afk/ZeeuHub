using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace WebApp.Services.ExcelImport;

// Persists uploaded import files on the web app file system for asynchronous processing.
public sealed class LocalExcelImportBackgroundFileStore : IExcelImportBackgroundFileStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".xlsm", ".csv"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalExcelImportBackgroundFileStore> _logger;
    private readonly ExcelImportBackgroundFileStoreOptions _options;
    private readonly Lazy<string> _storageRoot;

    public LocalExcelImportBackgroundFileStore(
        IWebHostEnvironment environment,
        IOptions<ExcelImportBackgroundFileStoreOptions> options,
        ILogger<LocalExcelImportBackgroundFileStore> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
        _storageRoot = new Lazy<string>(ResolveStorageRoot, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<StoredExcelImportFile> SaveAsync(IFormFile file, Guid companyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (companyId == Guid.Empty)
            throw new InvalidOperationException("Aktivt bolag saknas för Excelimporten.");
        if (file.Length <= 0 || file.Length > ExcelImportResourceLimits.MaxUploadBytes)
            throw new InvalidOperationException("Excelimportfilens storlek är inte tillåten.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Excelimportfilens filtyp stöds inte.");

        var storageRoot = GetCompanyStorageRoot(companyId);
        Directory.CreateDirectory(storageRoot);

        var storedPath = Path.Combine(storageRoot, $"{Guid.NewGuid():N}{extension}");
        await using (var output = new FileStream(
                         storedPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            try
            {
                await CopyWithLimitAsync(file, output, cancellationToken);
            }
            catch
            {
                output.Close();
                DeleteQuietly(storedPath);
                throw;
            }
        }

        return new StoredExcelImportFile
        {
            Path = storedPath,
            OriginalFileName = Path.GetFileName(file.FileName),
            SizeBytes = file.Length
        };
    }

    public void DeleteQuietly(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var storageRoot = Path.GetFullPath(GetStorageRoot()) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(storageRoot, comparison))
            {
                _logger.LogWarning("Refused to delete a temporary Excel import file outside the storage root.");
                return;
            }

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not delete temporary Excel import file {FileName}.",
                Path.GetFileName(path));
        }
    }

    public int CleanupExpired(
        DateTime cutoffUtc,
        IReadOnlySet<string> protectedPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectedPaths);
        var storageRoot = GetStorageRoot();
        if (!Directory.Exists(storageRoot))
            return 0;

        var deleted = 0;
        try
        {
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var path in Directory.EnumerateFiles(storageRoot, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!IsWithinStorageRoot(path))
                    {
                        _logger.LogWarning("Refused to clean an Excel import file outside the storage root.");
                        continue;
                    }

                    if (protectedPaths.Contains(Path.GetFullPath(path)))
                        continue;

                    var file = new FileInfo(path);
                    if (file.LinkTarget is not null || file.LastWriteTimeUtc >= cutoffUtc)
                        continue;

                    file.Delete();
                    deleted++;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not remove an expired Excel import background file.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Could not remove an expired Excel import background file due to access restrictions.");
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            return deleted;
        }

        return deleted;
    }

    private string GetStorageRoot()
        => _storageRoot.Value;

    private bool IsWithinStorageRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var storageRoot = Path.GetFullPath(GetStorageRoot()) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(storageRoot, comparison);
    }

    private string GetCompanyStorageRoot(Guid companyId)
        => Path.Combine(GetStorageRoot(), companyId.ToString("N"));

    private string ResolveStorageRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.StorageRoot))
            return Path.GetFullPath(_options.StorageRoot);

        if (_environment.IsProduction() && _options.RequireConfiguredStorageInProduction)
        {
            throw new ExcelImportStorageConfigurationException(
                "Excelimportens bakgrundslagring måste konfigureras till ett delat beständigt filsystem i produktion.");
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "ExcelImportJobs"));
    }

    private static async Task CopyWithLimitAsync(
        IFormFile file,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        var buffer = new byte[64 * 1024];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            totalBytes += bytesRead;
            if (totalBytes > ExcelImportResourceLimits.MaxUploadBytes)
                throw new InvalidOperationException("Excelimportfilens storlek är inte tillåten.");

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        if (totalBytes != file.Length)
            throw new InvalidOperationException("Excelimportfilens rapporterade storlek stämmer inte med filinnehållet.");
    }
}
