namespace WebApp.Services.Telemetry;

// This facade preserves the existing ITelemetryService contract for the application.
// Under the hood, telemetry responsibilities are split into focused services for AI, imports, sessions, and totals.
public sealed class TelemetryService : ITelemetryService
{
    private readonly ITelemetryExcelImportService _excelImportService;
    private readonly ITelemetryAiQueryService _aiQueryService;
    private readonly ITelemetryUsageService _usageService;
    private readonly ITelemetrySummaryService _summaryService;

    public TelemetryService(
        ITelemetryExcelImportService excelImportService,
        ITelemetryAiQueryService aiQueryService,
        ITelemetryUsageService usageService,
        ITelemetrySummaryService summaryService)
    {
        _excelImportService = excelImportService;
        _aiQueryService = aiQueryService;
        _usageService = usageService;
        _summaryService = summaryService;
    }

    public Task LogExcelImportAsync(Guid? companyId, string? userId, string? fileName, long fileSizeBytes, string? importType, int totalRows, int validRows, int invalidRows)
        => _excelImportService.LogExcelImportAsync(companyId, userId, fileName, fileSizeBytes, importType, totalRows, validRows, invalidRows);

    public Task LogAiQueryAsync(
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
        Models.Telemetry.AiQueryTelemetryDetails? details = null)
        => _aiQueryService.LogAiQueryAsync(companyId, userId, question, allowed, wasSuccessful, sqlText, errorMessage, promptTokens, completionTokens, totalTokens, details);

    public Task<(int ExcelImports, int AiQueries, double SessionMinutes)> GetTotalsAsync(int? daysBack = 30)
        => _summaryService.GetTotalsAsync(daysBack);

    public Task<int> GetTotalSessionMinutesAsync()
        => _usageService.GetTotalSessionMinutesAsync();

    public Task AddUsageAsync(string userId, Guid? companyId, int minutesIncrement, DateTime lastSeenUtc, bool ensureRecord = false)
        => _usageService.AddUsageAsync(userId, companyId, minutesIncrement, lastSeenUtc, ensureRecord);

    public Task<WebApp.ViewModels.Admin.PortalSessionsPageVm> GetPortalSessionsAsync(int? daysBack = 30, int take = 50)
        => _usageService.GetPortalSessionsAsync(daysBack, take);

    public Task<WebApp.ViewModels.Admin.ExcelImportsPageVm> GetExcelImportsAsync(int daysBack = 30, int take = 50)
        => _excelImportService.GetExcelImportsAsync(daysBack, take);

    public Task<WebApp.ViewModels.Admin.AiQueriesPageVm> GetAiQueriesAsync(int daysBack = 30, int latestPage = 1, int latestPageSize = 10)
        => _aiQueryService.GetAiQueriesAsync(daysBack, latestPage, latestPageSize);
}
