using WebApp.ViewModels.Admin;

namespace WebApp.Services.Telemetry;

// Handles portal session usage tracking and session-oriented admin telemetry views.
public interface ITelemetryUsageService
{
    Task<int> GetTotalSessionMinutesAsync();
    Task AddUsageAsync(string userId, Guid? companyId, int minutesIncrement, DateTime lastSeenUtc, bool ensureRecord = false);
    Task<PortalSessionsPageVm> GetPortalSessionsAsync(int? daysBack = 30, int take = 50);
}
