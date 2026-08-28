using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.ActionCenter;

public interface IActionCenterSeenStore
{
    Task<DateTime?> GetLastSeenUtcAsync(CancellationToken cancellationToken);
    Task SetLastSeenUtcAsync(DateTime utcTimestamp, CancellationToken cancellationToken);
}
