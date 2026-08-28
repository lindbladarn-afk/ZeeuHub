using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

public interface IAdminEventLogService
{
    Task<PortalEventLogsPageVm> GetPortalEventLogsAsync(
        int daysBack = 7,
        string? module = null,
        string? severity = null,
        Guid? companyId = null,
        string? search = null,
        int latestPage = 1,
        int latestPageSize = 10,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePortalEventLogAsync(Guid id, CancellationToken cancellationToken = default);
}
