using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Provides demo-only template and analytics data for NotifyMe without touching the legacy SQL engine.
public interface INotifyMeDemoService
{
    Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(int? companyCode, string? search = null, string? category = null, CancellationToken cancellationToken = default);
    Task<NotifyMeStatisticsVm> GetStatisticsAsync(int? companyCode, CancellationToken cancellationToken = default);
    NotifyMeTemplateVm? GetTemplate(string? templateKey);
}
