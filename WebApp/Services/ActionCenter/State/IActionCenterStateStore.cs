using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.ActionCenter;

namespace WebApp.Services.ActionCenter;

public interface IActionCenterStateStore
{
    Task<IReadOnlyList<ActionCenterItemState>> GetStatesAsync(Guid? companyId, string userId, CancellationToken cancellationToken);
    Task UpsertAsync(string externalId, ActionCenterItemStatus status, Guid? companyId, string userId, ActionCenterUpdateRequest snapshot, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionCenterItemState>> GetHistoryAsync(Guid? companyId, string userId, int take, CancellationToken cancellationToken);
}
