using Microsoft.Extensions.Caching.Memory;
using WebApp.Models.Application;

namespace WebApp.Services.ExcelImport;

// Keeps the visible Excel import list in process memory so it disappears when the app restarts or the cache is cleared.
public interface IExcelImportTransientStatusStore
{
    void Record(SidebarRuntimeEventRecord record);
    IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecent(Guid companyId, int take = 5);
    IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecentSummaries(Guid companyId, int take = 5);
    void ClearCompany(Guid companyId);
}

public sealed class ExcelImportTransientStatusStore : IExcelImportTransientStatusStore
{
    private const string CacheKeyPrefix = "excel-import:transient-status:";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromHours(2);
    private const int MaxItemsPerCompany = 8;
    private const int MaxRowsPerItem = 50;

    private readonly IMemoryCache _cache;

    public ExcelImportTransientStatusStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Record(SidebarRuntimeEventRecord record)
    {
        if (record.CompanyId == Guid.Empty)
            return;

        var cacheKey = GetCacheKey(record.CompanyId);
        var state = GetOrCreateState(cacheKey);

        lock (state.Sync)
        {
            state.Items[GetItemKey(record)] = Clone(record);
            Prune(state);
        }

        _cache.Set(cacheKey, state, CreateOptions());
    }

    public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecent(Guid companyId, int take = 5)
        => ListRecent(companyId, take, includeRows: true);

    public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecentSummaries(Guid companyId, int take = 5)
        => ListRecent(companyId, take, includeRows: false);

    private IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecent(Guid companyId, int take, bool includeRows)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<SidebarRuntimeStatusItemViewModel>();

        if (!_cache.TryGetValue(GetCacheKey(companyId), out CompanyState? state) || state is null)
            return Array.Empty<SidebarRuntimeStatusItemViewModel>();

        var safeTake = Math.Max(1, take);
        lock (state.Sync)
        {
            return state.Items.Values
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(safeTake)
                .Select(item => Map(item, includeRows))
                .ToList();
        }
    }

    public void ClearCompany(Guid companyId)
    {
        if (companyId == Guid.Empty)
            return;

        _cache.Remove(GetCacheKey(companyId));
    }

    private CompanyState GetOrCreateState(string cacheKey)
        => _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = AbsoluteExpiration;
            return new CompanyState();
        })!;

    private static MemoryCacheEntryOptions CreateOptions()
        => new()
        {
            SlidingExpiration = SlidingExpiration,
            AbsoluteExpirationRelativeToNow = AbsoluteExpiration
        };

    private static void Prune(CompanyState state)
    {
        var staleCutoff = DateTime.UtcNow.Subtract(AbsoluteExpiration);
        var staleKeys = state.Items
            .Where(item => item.Value.OccurredAtUtc < staleCutoff)
            .Select(item => item.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            state.Items.Remove(key);
        }

        while (state.Items.Count > MaxItemsPerCompany)
        {
            var oldest = state.Items
                .OrderBy(item => item.Value.OccurredAtUtc)
                .FirstOrDefault();

            if (oldest.Value is null)
                break;

            state.Items.Remove(oldest.Key);
        }
    }

    private static SidebarRuntimeEventRecord Clone(SidebarRuntimeEventRecord record)
        => new()
        {
            CompanyId = record.CompanyId,
            AggregateKey = record.AggregateKey,
            ImportBatchId = record.ImportBatchId,
            SourceFileName = record.SourceFileName,
            StartedAtUtc = record.StartedAtUtc,
            TotalRows = record.TotalRows,
            ValidRows = record.ValidRows,
            InvalidRows = record.InvalidRows,
            StagedRows = record.StagedRows,
            DurationLabel = record.DurationLabel,
            Source = record.Source,
            Title = record.Title,
            StatusLabel = record.StatusLabel,
            StatusTone = record.StatusTone,
            IconClass = record.IconClass,
            LinkUrl = record.LinkUrl,
            Summary = record.Summary,
            OccurredAtUtc = record.OccurredAtUtc,
            ColumnHeaders = (record.ColumnHeaders ?? new List<string>()).ToList(),
            ImportedRows = (record.ImportedRows ?? new List<ExcelImportRuntimeRowViewModel>()).Take(MaxRowsPerItem).Select(CloneRow).ToList(),
            VoucherPostingDate = record.VoucherPostingDate,
            VoucherReversalDate = record.VoucherReversalDate
        };

    private static SidebarRuntimeStatusItemViewModel Map(SidebarRuntimeEventRecord record, bool includeRows)
        => new()
        {
            OccurredAtUtc = record.OccurredAtUtc,
            AggregateKey = record.AggregateKey,
            ImportBatchId = record.ImportBatchId,
            SourceFileName = record.SourceFileName,
            StartedAtUtc = record.StartedAtUtc,
            TotalRows = record.TotalRows,
            ValidRows = record.ValidRows,
            InvalidRows = record.InvalidRows,
            StagedRows = record.StagedRows,
            DurationLabel = record.DurationLabel,
            Source = record.Source,
            Title = record.Title,
            Summary = record.Summary,
            LinkUrl = record.LinkUrl,
            StatusLabel = record.StatusLabel,
            StatusTone = record.StatusTone,
            TimeLabel = record.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"),
            IconClass = record.IconClass,
            ColumnHeaders = (record.ColumnHeaders ?? new List<string>()).ToList(),
            ImportedRows = includeRows
                ? (record.ImportedRows ?? new List<ExcelImportRuntimeRowViewModel>()).Take(MaxRowsPerItem).Select(CloneRow).ToList()
                : new List<ExcelImportRuntimeRowViewModel>(),
            VoucherPostingDate = record.VoucherPostingDate,
            VoucherReversalDate = record.VoucherReversalDate
        };

    private static ExcelImportRuntimeRowViewModel CloneRow(ExcelImportRuntimeRowViewModel row)
        => new()
        {
            RowNo = row.RowNo,
            IsValid = row.IsValid,
            ErrorMessage = row.ErrorMessage,
            Cells = new Dictionary<string, string>(row.Cells ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        };

    private static string GetCacheKey(Guid companyId)
        => $"{CacheKeyPrefix}{companyId:N}";

    private static string GetItemKey(SidebarRuntimeEventRecord record)
        => record.AggregateKey
           ?? $"{record.Source}:{record.Title}:{record.OccurredAtUtc:O}";

    private sealed class CompanyState
    {
        public object Sync { get; } = new();
        public Dictionary<string, SidebarRuntimeEventRecord> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
