using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;
using WebApp.Services.Admin;
using WebApp.Services.Application;

namespace WebApp.Services.Operations;

// This provider translates platform health checks into ZeeU-internal operational signals.
// It always emits one status item so the internal operations panel stays informative even when the platform is healthy.
public sealed class PlatformHealthInternalInsightProvider : IInsightProvider
{
    private readonly IAdminOverviewService _adminOverviewService;

    public string ProviderKey => "internal-platform-health";
    public ActionCenterAudience Audience => ActionCenterAudience.InternalAdmin;

    public PlatformHealthInternalInsightProvider(IAdminOverviewService adminOverviewService)
    {
        _adminOverviewService = adminOverviewService;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var statuses = await _adminOverviewService.GetHealthAsync();
        var now = DateTime.UtcNow;
        var unhealthy = statuses.Where(x => !x.Pending && !x.IsHealthy).ToList();

        if (unhealthy.Count == 0)
        {
            return new[]
            {
                new ActionCenterInsight
                {
                    Key = "internal-platform-health-ok",
                    Audience = ActionCenterAudience.InternalAdmin,
                    Category = "Drift",
                    Status = ActionCenterStatus.Open,
                    Priority = ActionCenterPriority.Info,
                    Title = "Nyckelanslutningar svarar normalt",
                    Description = "PortalIdentity DB och Jeeves DB svarar på senaste hälsokontrollen.",
                    DetectedAt = now,
                    LinkText = "Visa anslutningshälsa",
                    LinkUrl = "/Admin/HealthDetail"
                }
            };
        }

        var names = string.Join(", ", unhealthy.Select(x => x.Name));
        return new[]
        {
            new ActionCenterInsight
            {
                Key = "internal-platform-health-alert",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "Drift",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                Title = unhealthy.Count == 1
                    ? "1 nyckelanslutning har driftfel"
                    : $"{unhealthy.Count} nyckelanslutningar har driftfel",
                Description = $"Följande anslutningar svarar inte korrekt: {names}. Verifiera nät, credentials och aktuell databasstatus.",
                DetectedAt = now,
                LinkText = "Visa anslutningshälsa",
                LinkUrl = "/Admin/HealthDetail"
            }
        };
    }
}
