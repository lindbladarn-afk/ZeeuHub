using WebApp.Models.Application;

namespace WebApp.Services.ExcelImport;

// Splits Excel import rows into a filtered subset and a single visible page.
public static class ExcelImportRowPaging
{
    public const int DefaultPageSize = 50;

    public static ExcelImportRowPageResult Build(
        IReadOnlyList<ExcelImportRowResult> rows,
        int page,
        int pageSize,
        bool showOnlyInvalidRows,
        bool showAllRows = false)
    {
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var filteredRows = (rows ?? Array.Empty<ExcelImportRowResult>())
            .Where(row => !showOnlyInvalidRows || !row.IsValid)
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredRows.Count / (double)safePageSize));
        var safePage = Math.Clamp(page, 1, totalPages);
        var pageStart = (safePage - 1) * safePageSize;
        var pageRows = filteredRows
            .Skip(pageStart)
            .Take(safePageSize)
            .ToList();

        return new ExcelImportRowPageResult
        {
            AllRows = (rows ?? Array.Empty<ExcelImportRowResult>()).ToList(),
            FilteredRows = filteredRows,
            PageRows = pageRows,
            Page = safePage,
            PageSize = safePageSize,
            TotalPages = totalPages,
            TotalCount = rows?.Count ?? 0,
            FilteredCount = filteredRows.Count,
            ShowOnlyInvalidRows = showOnlyInvalidRows,
            ShowAllRows = showAllRows
        };
    }
}

public sealed class ExcelImportRowPageResult
{
    public List<ExcelImportRowResult> AllRows { get; init; } = new();
    public List<ExcelImportRowResult> FilteredRows { get; init; } = new();
    public List<ExcelImportRowResult> PageRows { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public bool ShowOnlyInvalidRows { get; init; }
    public bool ShowAllRows { get; init; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
