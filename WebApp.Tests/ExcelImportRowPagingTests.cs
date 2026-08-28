using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

public sealed class ExcelImportRowPagingTests
{
    [Fact]
    public void Build_Filters_InvalidRows_And_Paginates()
    {
        var rows = Enumerable.Range(1, 120)
            .Select(index => new ExcelImportRowResult
            {
                RowNo = index,
                IsValid = index % 3 != 0,
                Data = new Dictionary<string, string> { ["A"] = index.ToString() }
            })
            .ToList();

        var result = ExcelImportRowPaging.Build(rows, page: 2, pageSize: 50, showOnlyInvalidRows: true);

        Assert.Equal(120, result.TotalCount);
        Assert.Equal(40, result.FilteredCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(40, result.PageRows.Count);
        Assert.All(result.PageRows, row => Assert.False(row.IsValid));
    }

    [Fact]
    public void Build_Clamps_Page_And_PageSize()
    {
        var rows = Enumerable.Range(1, 10)
            .Select(index => new ExcelImportRowResult
            {
                RowNo = index,
                IsValid = true,
                Data = new Dictionary<string, string> { ["A"] = index.ToString() }
            })
            .ToList();

        var result = ExcelImportRowPaging.Build(rows, page: 99, pageSize: 0, showOnlyInvalidRows: false);

        Assert.Equal(10, result.TotalCount);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(10, result.TotalPages);
        Assert.Equal(10, result.Page);
        Assert.Single(result.PageRows);
    }

    [Fact]
    public void Build_ShowAllRows_Keeps_PageSize_And_Enables_BrowseMode()
    {
        var rows = Enumerable.Range(1, 250)
            .Select(index => new ExcelImportRowResult
            {
                RowNo = index,
                IsValid = index % 5 != 0,
                Data = new Dictionary<string, string> { ["A"] = index.ToString() }
            })
            .ToList();

        var result = ExcelImportRowPaging.Build(rows, page: 1, pageSize: 50, showOnlyInvalidRows: false, showAllRows: true);

        Assert.True(result.ShowAllRows);
        Assert.Equal(250, result.FilteredCount);
        Assert.Equal(50, result.PageSize);
        Assert.Equal(5, result.TotalPages);
        Assert.Equal(50, result.PageRows.Count);
    }
}
