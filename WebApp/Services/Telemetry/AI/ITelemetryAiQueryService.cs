using WebApp.ViewModels.Admin;
using WebApp.Models.Telemetry;

namespace WebApp.Services.Telemetry;

// Handles telemetry write/read flows for AI questions and admin AI telemetry views.
public interface ITelemetryAiQueryService
{
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

    Task<AiQueriesPageVm> GetAiQueriesAsync(int daysBack = 30, int latestPage = 1, int latestPageSize = 10);
}
