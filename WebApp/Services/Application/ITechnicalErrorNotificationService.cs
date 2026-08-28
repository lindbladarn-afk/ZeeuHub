using WebApp.Models.Application;

namespace WebApp.Services.Application;

public interface ITechnicalErrorNotificationService
{
    Task NotifyAsync(TechnicalErrorNotificationRequest request, CancellationToken cancellationToken = default);
}
