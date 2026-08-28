using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;
using WebApp.Services.Application.AI.Quota;

namespace WebApp.Services.Operations;

// This provider exposes ZeeU-internal AI governance signals.
// It is intentionally admin-only because it summarizes status across multiple companies.
public sealed class AiQuotaInternalInsightProvider : IInsightProvider
{
    private readonly IAiQuotaAdminService _aiQuotaAdminService;

    public string ProviderKey => "internal-ai-quota";
    public ActionCenterAudience Audience => ActionCenterAudience.InternalAdmin;

    public AiQuotaInternalInsightProvider(IAiQuotaAdminService aiQuotaAdminService)
    {
        _aiQuotaAdminService = aiQuotaAdminService;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var snapshot = await _aiQuotaAdminService.GetSnapshotAsync(ct: cancellationToken);
        var now = DateTime.UtcNow;

        var paidCompanies = snapshot.Companies
            .Where(x => string.Equals(x.CurrentPeriodMode, "paid", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PaidExtraBillableSekCurrentPeriod)
            .ToList();

        var blockedCompanies = snapshot.Companies
            .Where(x => string.Equals(x.CurrentPeriodMode, "blocked", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UsagePercentCurrentPeriod)
            .ToList();

        var warningCompanies = snapshot.Companies
            .Where(x => x.EffectiveEnabled)
            .Where(x => string.Equals(x.CurrentPeriodMode, "standard", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.UsagePercentCurrentPeriod >= x.EffectiveWarningThresholdPercent)
            .OrderByDescending(x => x.UsagePercentCurrentPeriod)
            .ToList();

        var insights = new System.Collections.Generic.List<ActionCenterInsight>();

        if (blockedCompanies.Count > 0)
        {
            var top = string.Join(", ", blockedCompanies.Take(3).Select(x => x.CompanyName));
            insights.Add(new ActionCenterInsight
            {
                Key = "internal-ai-quota-blocked",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "AI-kvot",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                Title = blockedCompanies.Count == 1
                    ? "1 bolag är blockerat av AI-kvoten"
                    : $"{blockedCompanies.Count} bolag är blockerade av AI-kvoten",
                Description = $"Bolag som kräver uppföljning: {top}. Öppna AI-kvotstyrning och avgör om de ska återställas eller gå till betalläge.",
                DetectedAt = now,
                LinkText = "Öppna AI-kontrollpanel",
                LinkUrl = "/Admin/AiQueries?tab=quota"
            });
        }

        if (paidCompanies.Count > 0)
        {
            var totalBillable = paidCompanies.Sum(x => x.PaidExtraBillableSekCurrentPeriod);
            var top = string.Join(", ", paidCompanies.Take(3).Select(x => x.CompanyName));
            insights.Add(new ActionCenterInsight
            {
                Key = "internal-ai-quota-paid",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "AI-betalläge",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.Medium,
                Title = paidCompanies.Count == 1
                    ? "1 bolag kör AI i betalläge"
                    : $"{paidCompanies.Count} bolag kör AI i betalläge",
                Description = $"Debiterbart extra belopp denna period: {totalBillable:N2} kr. Störst förbrukning just nu: {top}.",
                DetectedAt = now,
                LinkText = "Visa fakturaunderlag",
                LinkUrl = "/Admin/AiQueries?tab=billing"
            });
        }

        if (warningCompanies.Count > 0)
        {
            var top = string.Join(", ", warningCompanies.Take(3).Select(x => $"{x.CompanyName} ({x.UsagePercentCurrentPeriod}%)"));
            insights.Add(new ActionCenterInsight
            {
                Key = "internal-ai-quota-warning",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "AI-kvot",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.Medium,
                Title = warningCompanies.Count == 1
                    ? "1 bolag närmar sig AI-kvotgränsen"
                    : $"{warningCompanies.Count} bolag närmar sig AI-kvotgränsen",
                Description = $"Bolag som bör följas upp: {top}. Kontrollera om standardgränserna är rätt eller om bolagen behöver override.",
                DetectedAt = now,
                LinkText = "Öppna AI-kvotstyrning",
                LinkUrl = "/Admin/AiQueries?tab=quota"
            });
        }

        return insights;
    }
}
