using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Telemetry;

// Owns user session usage tracking and admin session reporting.
public sealed class TelemetryUsageService : ITelemetryUsageService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TelemetryUsageService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<int> GetTotalSessionMinutesAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var minutes = await db.UserUsageTotals!
            .Select(x => x.TotalMinutes)
            .SumAsync();
        return minutes;
    }

    public async Task AddUsageAsync(string userId, Guid? companyId, int minutesIncrement, DateTime lastSeenUtc, bool ensureRecord = false)
    {
        if (string.IsNullOrWhiteSpace(userId) || minutesIncrement < 0 || companyId is null)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var existing = await db.UserUsageTotals!
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CompanyId == companyId.Value);

        if (existing is null)
        {
            if (!ensureRecord && minutesIncrement == 0)
            {
                return;
            }

            existing = new Models.Telemetry.UserUsageTotal
            {
                UserId = userId,
                CompanyId = companyId.Value,
                TotalMinutes = Math.Max(minutesIncrement, 0),
                LastSeenAtUtc = lastSeenUtc,
                LastUpdatedAtUtc = lastSeenUtc
            };
            await db.UserUsageTotals!.AddAsync(existing);
        }
        else
        {
            if (minutesIncrement > 0)
            {
                existing.TotalMinutes += minutesIncrement;
            }

            existing.LastSeenAtUtc = lastSeenUtc;
            existing.LastUpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task<PortalSessionsPageVm> GetPortalSessionsAsync(int? daysBack = 30, int take = 50)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        DateTime since = DateTime.MinValue;
        var baseQuery = db.UserUsageTotals!.AsNoTracking().AsQueryable();
        if (daysBack.HasValue && daysBack.Value > 0)
        {
            since = DateTime.UtcNow.AddDays(-daysBack.Value);
            baseQuery = baseQuery.Where(x => x.LastUpdatedAtUtc >= since);
        }

        var latest = await baseQuery
            .OrderByDescending(x => x.LastUpdatedAtUtc)
            .Take(take * 2)
            .ToListAsync();

        var companyIds = latest.Select(x => x.CompanyId).Distinct().ToList();
        var userIds = latest.Select(x => x.UserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

        var companies = await db.Companies!
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name ?? "-");
        var users = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);

        var latestVm = latest.Select(x => new PortalSessionEntryVm
        {
            UserId = x.UserId,
            UserEmail = x.UserId != null && users.TryGetValue(x.UserId, out var email) ? email : x.UserId,
            CompanyId = x.CompanyId,
            CompanyName = companies.TryGetValue(x.CompanyId, out var name) ? name : "-",
            StartedAtUtc = x.LastUpdatedAtUtc,
            LastSeenAtUtc = x.LastSeenAtUtc,
            DurationMinutes = x.TotalMinutes
        })
        .GroupBy(x => x.UserId ?? x.UserEmail ?? string.Empty)
        .Select(g => g.OrderByDescending(x => x.LastSeenAtUtc).First())
        .OrderByDescending(x => x.LastSeenAtUtc)
        .Take(take)
        .ToList();

        var topCompanies = await baseQuery
            .GroupBy(x => x.CompanyId)
            .Select(g => new
            {
                CompanyId = g.Key,
                Sessions = g.Count(),
                Minutes = g.Sum(x => (int?)x.TotalMinutes) ?? 0
            })
            .OrderByDescending(x => x.Minutes)
            .Take(10)
            .ToListAsync();

        var topCompaniesVm = topCompanies.Select(x => new PortalSessionSummaryVm
        {
            CompanyId = x.CompanyId,
            CompanyName = companies.TryGetValue(x.CompanyId, out var name) ? name : "-",
            Sessions = x.Sessions,
            TotalMinutes = x.Minutes
        }).ToList();

        return new PortalSessionsPageVm
        {
            TotalSessions = await baseQuery.CountAsync(),
            TotalMinutes = await baseQuery.Select(x => x.TotalMinutes).SumAsync(),
            TopCompanies = topCompaniesVm,
            Latest = latestVm
        };
    }
}
