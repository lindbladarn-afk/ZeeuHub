using WebApp.ViewModels.NotifyMe;

namespace WebApp.Repositories.NotifyMe;

// Repository contract for portal-owned NotifyMe configuration and runtime history.
public interface INotifyMeRepository
{
    Task<IReadOnlyList<NotifyMeListItemVm>> GetNotificationsAsync(string connectionString, int companyCode, CancellationToken cancellationToken = default);
    Task<NotifyMeDetailsVm?> GetNotificationAsync(string connectionString, int companyCode, int notificationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotifyMeLogItemVm>> GetRecentLogEntriesAsync(string connectionString, int companyCode, int? notificationId = null, int take = 15, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetTypeOptionsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotifyMeLookupOptionVm>> GetPriorityOptionsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<int> SaveNotificationAsync(string connectionString, int companyCode, NotifyMeDraftVm draft, string updatedBy, CancellationToken cancellationToken = default);
}
