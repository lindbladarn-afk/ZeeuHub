using Microsoft.EntityFrameworkCore;
using WebApp.Data;

namespace WebApp.Services.Telemetry;

// Computes high-level admin totals across telemetry domains.
public sealed class TelemetrySummaryService : ITelemetrySummaryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TelemetrySummaryService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(int ExcelImports, int AiQueries, double SessionMinutes)> GetTotalsAsync(int? daysBack = 30)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var limit = daysBack.HasValue && daysBack.Value > 0;
        DateTime since = DateTime.MinValue;
        if (limit)
        {
            since = DateTime.UtcNow.AddDays(-daysBack!.Value);
        }

        var excelQuery = db.ExcelImportLogs!.AsQueryable();
        var aiQuery = db.AiQueryLogs!.AsQueryable();
        var sessionQuery = db.UserUsageTotals!.AsQueryable();

        if (limit)
        {
            excelQuery = excelQuery.Where(x => x.CreatedAtUtc >= since);
            aiQuery = aiQuery.Where(x => x.CreatedAtUtc >= since);
            sessionQuery = sessionQuery.Where(x => x.LastUpdatedAtUtc >= since);
        }

        var excelCount = await excelQuery.CountAsync();
        var aiCount = await aiQuery.CountAsync();
        var sessionMinutesNullable = await sessionQuery
            .Select(x => (int?)x.TotalMinutes)
            .SumAsync();
        int sessionMinutes = sessionMinutesNullable ?? 0;
        return (excelCount, aiCount, sessionMinutes);
    }
}
