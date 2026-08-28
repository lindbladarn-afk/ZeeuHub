using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Builds read-only NotifyMe page models from repository data.
public interface INotifyMePageQueryService
{
    Task<NotifyMeOverviewVm> GetOverviewAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? status = null,
        string? type = null,
        string? priority = null,
        int page = 1,
        CancellationToken cancellationToken = default);

    Task<NotifyMeStatisticsVm> GetStatisticsAsync(string? connectionString, int? companyCode, CancellationToken cancellationToken = default);

    Task<NotifyMeHistoryPageVm> GetHistoryAsync(
        string? connectionString,
        int? companyCode,
        int? historyNotificationId = null,
        string? historySearch = null,
        int page = 1,
        CancellationToken cancellationToken = default);

    Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    Task<NotifyMeDetailsPageVm> GetDetailsAsync(string? connectionString, int? companyCode, int notificationId, CancellationToken cancellationToken = default);
}
