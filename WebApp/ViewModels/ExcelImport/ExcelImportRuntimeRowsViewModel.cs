using WebApp.Services.ExcelImport;

namespace WebApp.ViewModels.ExcelImport;

// Carries one server-paged runtime row table for an Excel import status card.
public sealed class ExcelImportRuntimeRowsViewModel
{
    public string AggregateKey { get; init; } = string.Empty;
    public string ImportType { get; init; } = string.Empty;
    public List<string> Headers { get; init; } = new();
    public List<ExcelImportRowResult> Rows { get; init; } = new();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = ExcelImportRowPaging.DefaultPageSize;
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public int TotalPages { get; init; } = 1;
    public bool ShowOnlyInvalidRows { get; init; }
    public bool ShowAllRows { get; init; }
    public string? EditUrl { get; init; }
}
