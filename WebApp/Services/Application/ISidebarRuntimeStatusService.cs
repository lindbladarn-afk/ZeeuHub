using Entities.Application;
using WebApp.Models.Application;

namespace WebApp.Services.Application;

public interface ISidebarRuntimeStatusService
{
    SidebarRuntimeStatusViewModel GetStatus(UserSession? sessionUser);
    Task<SidebarRuntimeStatusViewModel> GetStatusAsync(UserSession? sessionUser, CancellationToken cancellationToken = default);
    void RecordEvent(UserSession sessionUser, SidebarRuntimeEventRecord record);
    void RecordEvent(Guid companyId, SidebarRuntimeEventRecord record);
    void MarkAllRead(UserSession sessionUser);
}
