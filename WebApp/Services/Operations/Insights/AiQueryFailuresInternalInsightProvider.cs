using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.Operations;

// This provider highlights failed AI questions across companies so ZeeU can follow up on prompt quality,
// schema issues, or tenant-specific problems before customers escalate them manually.
public sealed class AiQueryFailuresInternalInsightProvider : IInsightProvider
{
    private readonly ApplicationDbContext _db;

    public string ProviderKey => "internal-ai-query-failures";
    public ActionCenterAudience Audience => ActionCenterAudience.InternalAdmin;

    public AiQueryFailuresInternalInsightProvider(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-7);

        var failed = await _db.AiQueryLogs!
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since)
            .Where(x => !(x.WasSuccessful ?? x.WasAllowed))
            .GroupBy(x => x.CompanyId)
            .Select(g => new
            {
                CompanyId = g.Key,
                Count = g.Count(),
                LatestAtUtc = g.Max(x => x.CreatedAtUtc)
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (failed.Count == 0)
        {
            return Array.Empty<ActionCenterInsight>();
        }

        var companyIds = failed.Where(x => x.CompanyId.HasValue).Select(x => x.CompanyId!.Value).Distinct().ToList();
        var companies = await _db.Companies!
            .AsNoTracking()
            .Where(x => companyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? "-", cancellationToken);

        var totalFailed = failed.Sum(x => x.Count);
        var top = string.Join(", ", failed.Select(x =>
        {
            var name = x.CompanyId.HasValue && companies.TryGetValue(x.CompanyId.Value, out var companyName)
                ? companyName
                : "Okänt bolag";
            return $"{name} ({x.Count})";
        }));

        return new[]
        {
            new ActionCenterInsight
            {
                Key = "internal-ai-query-failures",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "AI-frågor",
                Status = ActionCenterStatus.Open,
                Priority = totalFailed >= 10 ? ActionCenterPriority.High : ActionCenterPriority.Medium,
                Title = totalFailed == 1
                    ? "1 AI-fråga misslyckades senaste veckan"
                    : $"{totalFailed} AI-frågor misslyckades senaste veckan",
                Description = $"Bolag att följa upp: {top}. Granska felorsaker och SQL-generering innan problemen växer vidare hos kund.",
                DetectedAt = failed.Max(x => x.LatestAtUtc),
                LinkText = "Öppna AI-översikt",
                LinkUrl = "/Admin/AiQueries?tab=overview"
            }
        };
    }
}
