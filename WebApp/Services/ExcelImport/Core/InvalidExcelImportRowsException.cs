namespace WebApp.Services.ExcelImport;

// Raised when edited Excel import rows are missing, empty, or cannot be parsed safely.
public sealed class InvalidExcelImportRowsException : Exception
{
    public InvalidExcelImportRowsException(string message)
        : base(message)
    {
    }

    public InvalidExcelImportRowsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
