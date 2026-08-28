using WebApp.Models.Telemetry;

namespace WebApp.Services.Telemetry;

public interface ITelemetryService
{
    Task LogExcelImportAsync(Guid? companyId, string? userId, string? fileName, long fileSizeBytes, string? importType, int totalRows, int validRows, int invalidRows);
    Task LogAiQueryAsync(
        Guid? companyId,
        string? userId,
        string? question,
        bool allowed,
        bool? wasSuccessful = null,
        string? sqlText = null,
        string? errorMessage = null,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        AiQueryTelemetryDetails? details = null);
    Task<(int ExcelImports, int AiQueries, double SessionMinutes)> GetTotalsAsync(int? daysBack = 30);
    Task<WebApp.ViewModels.Admin.PortalSessionsPageVm> GetPortalSessionsAsync(int? daysBack = 30, int take = 50);
    Task<int> GetTotalSessionMinutesAsync();
    Task AddUsageAsync(string userId, Guid? companyId, int minutesIncrement, DateTime lastSeenUtc, bool ensureRecord = false);
    Task<WebApp.ViewModels.Admin.ExcelImportsPageVm> GetExcelImportsAsync(int daysBack = 30, int take = 50);
    Task<WebApp.ViewModels.Admin.AiQueriesPageVm> GetAiQueriesAsync(int daysBack = 30, int latestPage = 1, int latestPageSize = 10);
}
