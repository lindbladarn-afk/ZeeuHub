using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.ExcelImport;

// Initializes Excel import staging tables on demand for internal admin use.
public interface IExcelImportTableInitializationService
{
    Task<ExcelImportTableInitializationResult> EnsureImportTablesAsync(CancellationToken cancellationToken);
}

public sealed class ExcelImportTableInitializationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<ExcelImportTableInitializationItem> Items { get; init; } = new List<ExcelImportTableInitializationItem>();
}

public sealed class ExcelImportTableInitializationItem
{
    public string TableName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
