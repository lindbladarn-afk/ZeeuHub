namespace WebApp.Services.ExcelImport;

// Configures the shared filesystem used by queued Excel import jobs.
public sealed class ExcelImportBackgroundFileStoreOptions
{
    public const string SectionName = "ExcelImport:BackgroundFileStore";

    public string? StorageRoot { get; set; }
    public bool RequireConfiguredStorageInProduction { get; set; } = true;
}
