using Microsoft.Extensions.Caching.Memory;
using WebApp.Models.Application;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the transient Excel import list keeps only the latest status per job and stays sorted.
public sealed class ExcelImportRuntimeStatusServiceTests
{
    [Fact]
    public void GetRecentItems_Returns_Most_Recent_Transient_Items()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new ExcelImportTransientStatusStore(cache);
        var service = new ExcelImportRuntimeStatusService(store);
        var companyId = Guid.NewGuid();
        var olderJobKey = Guid.NewGuid().ToString("N");
        var newerJobKey = Guid.NewGuid().ToString("N");

        store.Record(CreateRecord(companyId, $"excel-import:voucher:{olderJobKey}", "Äldre import", DateTime.UtcNow.AddMinutes(-2), "queued", "Pågår"));
        store.Record(CreateRecord(companyId, $"excel-import:voucher:{olderJobKey}", "Äldre import", DateTime.UtcNow.AddMinutes(-1), "completed", "Klart"));
        store.Record(CreateRecord(companyId, $"excel-import:budget:{newerJobKey}", "Nyare import", DateTime.UtcNow, "queued", "Pågår"));

        var result = service.GetRecentItems(companyId, take: 5);

        Assert.Equal(2, result.Count);
        Assert.Equal("Nyare import", result[0].Title);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), result[0].ImportBatchId);
        Assert.Equal("Äldre import", result[1].Title);
        Assert.Equal("Klart", result[1].StatusLabel);
        Assert.Single(result[1].ImportedRows);
        Assert.Equal("A1", result[1].ImportedRows[0].Cells["KolumnA"]);
    }

    [Fact]
    public void GetRecentItems_Returns_Empty_For_Missing_Company()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ExcelImportRuntimeStatusService(new ExcelImportTransientStatusStore(cache));

        var result = service.GetRecentItems(Guid.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void GetRecentItems_Ignores_Null_Row_Collections()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new ExcelImportTransientStatusStore(cache);
        var service = new ExcelImportRuntimeStatusService(store);
        var companyId = Guid.NewGuid();

        store.Record(new SidebarRuntimeEventRecord
        {
            CompanyId = companyId,
            AggregateKey = "excel-import:budget:job-1",
            Source = "ExcelImport",
            Title = "Budget",
            Summary = "Import av budget.xlsx klar.",
            LinkUrl = "/ExcelImport",
            StatusLabel = "Completed",
            StatusTone = "success",
            IconClass = "fa fa-file-excel",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ColumnHeaders = null!,
            ImportedRows = null!
        });

        var result = service.GetRecentItems(companyId, take: 5);

        Assert.Single(result);
        Assert.Empty(result[0].ImportedRows);
        Assert.Empty(result[0].ColumnHeaders);
    }

    [Fact]
    public void GetRecentSummaries_Leaves_Row_Data_Out_Of_Status_List()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new ExcelImportTransientStatusStore(cache);
        var service = new ExcelImportRuntimeStatusService(store);
        var companyId = Guid.NewGuid();

        store.Record(CreateRecord(companyId, "excel-import:budget:job-1", "Budget", DateTime.UtcNow, "success", "Completed"));

        var result = service.GetRecentSummaries(companyId, take: 5);

        Assert.Single(result);
        Assert.Equal("Budget", result[0].Title);
        Assert.Equal(new[] { "KolumnA", "KolumnB" }, result[0].ColumnHeaders);
        Assert.Empty(result[0].ImportedRows);
    }

    [Fact]
    public void GetRecentItems_Caps_Row_Data_To_Preview_Size()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new ExcelImportTransientStatusStore(cache);
        var service = new ExcelImportRuntimeStatusService(store);
        var companyId = Guid.NewGuid();
        var record = CreateRecord(companyId, "excel-import:budget:job-rows", "Budget", DateTime.UtcNow, "success", "Completed");
        record.ImportedRows = Enumerable.Range(1, 250)
            .Select(index => new ExcelImportRuntimeRowViewModel
            {
                RowNo = index,
                IsValid = true,
                Cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["KolumnA"] = index.ToString()
                }
            })
            .ToList();

        store.Record(record);

        var result = service.GetRecentItems(companyId, take: 5);

        Assert.Single(result);
        Assert.Equal(50, result[0].ImportedRows.Count);
        Assert.Equal(50, result[0].ImportedRows[^1].RowNo);
    }

    private static SidebarRuntimeEventRecord CreateRecord(Guid companyId, string aggregateKey, string title, DateTime occurredAtUtc, string statusTone, string statusLabel)
    {
        return new SidebarRuntimeEventRecord
        {
            CompanyId = companyId,
            AggregateKey = aggregateKey,
            ImportBatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Source = "ExcelImport",
            Title = title,
            Summary = $"{statusLabel} import",
            LinkUrl = "/ExcelImport",
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            IconClass = "fa fa-file-excel",
            OccurredAtUtc = occurredAtUtc,
            ColumnHeaders = new List<string> { "KolumnA", "KolumnB" },
            ImportedRows = new List<ExcelImportRuntimeRowViewModel>
            {
                new()
                {
                    RowNo = 1,
                    IsValid = true,
                    Cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["KolumnA"] = "A1",
                        ["KolumnB"] = "B1"
                    }
                }
            }
        };
    }
}
