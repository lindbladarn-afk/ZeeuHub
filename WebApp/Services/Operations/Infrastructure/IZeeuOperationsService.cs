using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;

namespace WebApp.Services.Operations;

// This service powers ZeeU's internal operations/control-center view.
// It is separate from the customer-facing ActionCenter so admin-only insights never bleed into tenant UI.
public interface IZeeuOperationsService
{
    Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken);
    Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken);
    void InvalidateCache(UserSession user);
}
