using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Service contract for the NotifyMe portal prototype.
public interface INotifyMeService
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
    Task<NotifyMeCreatePrototypeVm> GetCreatePrototypeAsync(string? connectionString, int? companyCode, int? notificationId = null, CancellationToken cancellationToken = default);
    Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(string? connectionString, int? companyCode, int notificationId, string overrideRecipient, CancellationToken cancellationToken = default);
    Task<int> SaveNotificationAsync(string? connectionString, int? companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default);
}
