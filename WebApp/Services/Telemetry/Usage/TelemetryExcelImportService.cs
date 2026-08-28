using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Telemetry;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Telemetry;

// Owns import telemetry writes and admin import reporting.
public sealed class TelemetryExcelImportService : ITelemetryExcelImportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TelemetryExcelImportService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task LogExcelImportAsync(Guid? companyId, string? userId, string? fileName, long fileSizeBytes, string? importType, int totalRows, int validRows, int invalidRows)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        await db.ExcelImportLogs!.AddAsync(new ExcelImportLog
        {
            CompanyId = companyId,
            UserId = userId,
            FileName = Trim(fileName, 256),
            FileSizeBytes = fileSizeBytes,
            ImportType = Trim(importType, 128),
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<ExcelImportsPageVm> GetExcelImportsAsync(int daysBack = 30, int take = 50)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var since = DateTime.UtcNow.AddDays(-daysBack);
        var baseQuery = db.ExcelImportLogs!
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since);

        var latest = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync();

        var companyIds = latest.Select(x => x.CompanyId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var userIds = latest.Select(x => x.UserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

        var companies = await db.Companies!
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name ?? "-");
        var users = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);

        var latestVm = latest.Select(x => new ExcelImportEntryVm
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            CompanyName = x.CompanyId != null && companies.TryGetValue(x.CompanyId.Value, out var name) ? name : "-",
            UserId = x.UserId,
            UserEmail = x.UserId != null && users.TryGetValue(x.UserId, out var email) ? email : x.UserId,
            FileName = x.FileName,
            ImportType = x.ImportType,
            FileSizeBytes = x.FileSizeBytes,
            TotalRows = x.TotalRows,
            InvalidRows = x.InvalidRows,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();

        var topCompanies = await baseQuery
            .GroupBy(x => x.CompanyId)
            .Select(g => new
            {
                CompanyId = g.Key,
                Count = g.Count(),
                Rows = g.Sum(x => x.TotalRows)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var topCompaniesVm = topCompanies.Select(x => new ExcelImportSummaryVm
        {
            CompanyId = x.CompanyId,
            CompanyName = x.CompanyId != null && companies.TryGetValue(x.CompanyId.Value, out var name) ? name : "-",
            Count = x.Count,
            TotalRows = x.Rows
        }).ToList();

        return new ExcelImportsPageVm
        {
            TotalImports = await baseQuery.CountAsync(),
            TotalRows = await baseQuery.Select(x => x.TotalRows).SumAsync(),
            TopCompanies = topCompaniesVm,
            Latest = latestVm
        };
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
