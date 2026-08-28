using WebApp.Models.Application;

namespace WebApp.Services.Application;

public interface IPortalEventLogService
{
    Task RecordAsync(PortalEventLogEntry entry, CancellationToken cancellationToken = default);
}
