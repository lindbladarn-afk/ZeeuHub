namespace WebApp.Services.Telemetry;

// Computes cross-domain admin totals without forcing the main telemetry facade to own every query directly.
public interface ITelemetrySummaryService
{
    Task<(int ExcelImports, int AiQueries, double SessionMinutes)> GetTotalsAsync(int? daysBack = 30);
}
