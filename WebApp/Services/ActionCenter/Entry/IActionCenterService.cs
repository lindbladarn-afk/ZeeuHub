using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;

namespace WebApp.Services.ActionCenter;

public interface IActionCenterService
{
    Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken);
    Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken);
    void InvalidateCache(UserSession user);
}
