namespace WebApp.Services.ExcelImport;

// Identifies an unsafe or missing storage configuration for queued Excel imports.
public sealed class ExcelImportStorageConfigurationException : InvalidOperationException
{
    public ExcelImportStorageConfigurationException(string message)
        : base(message)
    {
    }
}
