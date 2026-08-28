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

// This provider surfaces import quality issues across customers.
// The goal is to catch recurring bad files or onboarding problems centrally instead of waiting for support tickets.
public sealed class ExcelImportInternalInsightProvider : IInsightProvider
{
    private readonly ApplicationDbContext _db;

    public string ProviderKey => "internal-excel-imports";
    public ActionCenterAudience Audience => ActionCenterAudience.InternalAdmin;

    public ExcelImportInternalInsightProvider(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-7);

        var problematicImports = await _db.ExcelImportLogs!
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since)
            .Where(x => x.InvalidRows > 0)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (problematicImports.Count == 0)
        {
            return Array.Empty<ActionCenterInsight>();
        }

        var companyIds = problematicImports
            .Where(x => x.CompanyId.HasValue)
            .Select(x => x.CompanyId!.Value)
            .Distinct()
            .ToList();

        var companies = await _db.Companies!
            .AsNoTracking()
            .Where(x => companyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? "-", cancellationToken);

        var groupedCompanies = problematicImports
            .GroupBy(x => x.CompanyId)
            .Select(g => new
            {
                CompanyId = g.Key,
                InvalidRows = g.Sum(x => x.InvalidRows),
                Count = g.Count()
            })
            .OrderByDescending(x => x.InvalidRows)
            .Take(3)
            .ToList();

        var top = string.Join(", ", groupedCompanies.Select(x =>
        {
            var name = x.CompanyId.HasValue && companies.TryGetValue(x.CompanyId.Value, out var companyName)
                ? companyName
                : "Okänt bolag";
            return $"{name} ({x.InvalidRows} felrader)";
        }));

        var totalInvalidRows = problematicImports.Sum(x => x.InvalidRows);
        return new[]
        {
            new ActionCenterInsight
            {
                Key = "internal-excel-import-issues",
                Audience = ActionCenterAudience.InternalAdmin,
                Category = "Importer",
                Status = ActionCenterStatus.Open,
                Priority = totalInvalidRows >= 100 ? ActionCenterPriority.High : ActionCenterPriority.Medium,
                Title = problematicImports.Count == 1
                    ? "1 import innehåller felrader senaste veckan"
                    : $"{problematicImports.Count} importer innehåller felrader senaste veckan",
                Description = $"Totalt {totalInvalidRows} felrader registrerades. Störst påverkan just nu: {top}.",
                DetectedAt = problematicImports.Max(x => x.CreatedAtUtc),
                LinkText = "Öppna Excelimporter",
                LinkUrl = "/Admin/ExcelImports"
            }
        };
    }
}
