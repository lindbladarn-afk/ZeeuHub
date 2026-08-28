using WebApp.ViewModels.Admin;

namespace WebApp.Services.Telemetry;

// Handles telemetry write/read flows for Excel import activity.
public interface ITelemetryExcelImportService
{
    Task LogExcelImportAsync(Guid? companyId, string? userId, string? fileName, long fileSizeBytes, string? importType, int totalRows, int validRows, int invalidRows);
    Task<ExcelImportsPageVm> GetExcelImportsAsync(int daysBack = 30, int take = 50);
}
