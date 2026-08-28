using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

public interface INotifyMeExecutionService
{
    Task<NotifyMeTestRunResultVm> RunTestNotificationAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        string overrideRecipient,
        CancellationToken cancellationToken = default);

    Task RunScheduledNotificationAsync(
        string connectionString,
        int companyCode,
        int notificationId,
        CancellationToken cancellationToken = default);
}
