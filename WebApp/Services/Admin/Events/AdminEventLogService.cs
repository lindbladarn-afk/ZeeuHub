using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

public sealed class AdminEventLogService : IAdminEventLogService
{
    private static readonly int[] AllowedDaysBack = [1, 7, 30, 90];
    private const int DefaultLatestPageSize = 10;
    private readonly ApplicationDbContext _context;

    public AdminEventLogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PortalEventLogsPageVm> GetPortalEventLogsAsync(
        int daysBack = 7,
        string? module = null,
        string? severity = null,
        Guid? companyId = null,
        string? search = null,
        int latestPage = 1,
        int latestPageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedDaysBack = AllowedDaysBack.Contains(daysBack) ? daysBack : 7;
        var fromUtc = DateTime.UtcNow.AddDays(-normalizedDaysBack);
        var trimmedModule = string.IsNullOrWhiteSpace(module) ? null : module.Trim();
        var trimmedSeverity = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim();
        var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedPageSize = Math.Clamp(latestPageSize <= 0 ? DefaultLatestPageSize : latestPageSize, 1, 100);

        var baseQuery = _context.PortalEventLogs!
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc);

        var modules = await baseQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.Module))
            .Select(x => x.Module)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var severities = await baseQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.Severity))
            .Select(x => x.Severity)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var companyRows = await baseQuery
            .Where(x => x.CompanyId.HasValue)
            .GroupBy(x => new { x.CompanyId, x.CompanyName })
            .Select(g => new
            {
                CompanyId = g.Key.CompanyId!.Value,
                CompanyName = g.Key.CompanyName
            })
            .ToListAsync(cancellationToken);

        var companies = companyRows
            .Select(x => new PortalEventLogCompanyFilterOptionVm
            {
                CompanyId = x.CompanyId,
                Label = string.IsNullOrWhiteSpace(x.CompanyName)
                    ? x.CompanyId.ToString("D")
                    : x.CompanyName!
            })
            .OrderBy(x => x.Label)
            .ToList();

        var filteredQuery = baseQuery;

        if (!string.IsNullOrWhiteSpace(trimmedModule))
            filteredQuery = filteredQuery.Where(x => x.Module == trimmedModule);

        if (!string.IsNullOrWhiteSpace(trimmedSeverity))
            filteredQuery = filteredQuery.Where(x => x.Severity == trimmedSeverity);

        if (companyId.HasValue)
            filteredQuery = filteredQuery.Where(x => x.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            filteredQuery = filteredQuery.Where(x =>
                (x.Message != null && x.Message.Contains(trimmedSearch)) ||
                (x.Action != null && x.Action.Contains(trimmedSearch)) ||
                (x.CompanyName != null && x.CompanyName.Contains(trimmedSearch)) ||
                (x.UserEmail != null && x.UserEmail.Contains(trimmedSearch)) ||
                (x.RequestPath != null && x.RequestPath.Contains(trimmedSearch)));
        }

        var totalEvents = await filteredQuery.CountAsync(cancellationToken);
        var normalizedTotalPages = Math.Max(1, (int)Math.Ceiling(totalEvents / (double)normalizedPageSize));
        latestPage = Math.Clamp(latestPage, 1, normalizedTotalPages);
        var skip = (latestPage - 1) * normalizedPageSize;

        var latest = await filteredQuery
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(x => new PortalEventLogListItemVm
            {
                Id = x.Id,
                OccurredAtUtc = x.OccurredAtUtc,
                Module = x.Module,
                Action = x.Action,
                Severity = x.Severity,
                CompanyId = x.CompanyId,
                CompanyName = x.CompanyName,
                JeevesCompanyCode = x.JeevesCompanyCode,
                UserEmail = x.UserEmail,
                RequestPath = x.RequestPath,
                CorrelationId = x.CorrelationId,
                Message = x.Message,
                Exception = x.Exception,
                AdditionalData = x.AdditionalData
            })
            .ToListAsync(cancellationToken);

        var eventsLast24Hours = await filteredQuery.CountAsync(
            x => x.OccurredAtUtc >= DateTime.UtcNow.AddHours(-24),
            cancellationToken);
        var distinctModules = await filteredQuery
            .Select(x => x.Module)
            .Distinct()
            .CountAsync(cancellationToken);
        var distinctCompanies = await filteredQuery
            .Where(x => x.CompanyId.HasValue)
            .Select(x => x.CompanyId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new PortalEventLogsPageVm
        {
            DaysBack = normalizedDaysBack,
            Module = trimmedModule,
            Severity = trimmedSeverity,
            CompanyId = companyId,
            Search = trimmedSearch,
            TotalEvents = totalEvents,
            EventsLast24Hours = eventsLast24Hours,
            DistinctModules = distinctModules,
            DistinctCompanies = distinctCompanies,
            LatestPage = latestPage,
            LatestPageSize = normalizedPageSize,
            LatestTotalCount = totalEvents,
            AvailableModules = modules
                .Select(x => new PortalEventLogFilterOptionVm { Value = x, Label = x })
                .ToList(),
            AvailableSeverities = severities
                .Select(x => new PortalEventLogFilterOptionVm { Value = x, Label = x })
                .ToList(),
            AvailableCompanies = companies,
            Latest = latest
        };
    }

    public async Task<bool> DeletePortalEventLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.PortalEventLogs!
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        _context.PortalEventLogs!.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
