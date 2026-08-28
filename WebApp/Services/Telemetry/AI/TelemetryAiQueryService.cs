using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Telemetry;
using WebApp.Services.Application.AI;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Telemetry;

// Owns AI telemetry writes and admin AI telemetry reporting.
public sealed class TelemetryAiQueryService : ITelemetryAiQueryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TelemetryAiQueryService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task LogAiQueryAsync(
        Guid? companyId,
        string? userId,
        string? question,
        bool allowed,
        bool? wasSuccessful = null,
        string? sqlText = null,
        string? errorMessage = null,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        AiQueryTelemetryDetails? details = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        await db.AiQueryLogs!.AddAsync(new AiQueryLog
        {
            CompanyId = companyId,
            UserId = userId,
            Question = Trim(question, 2000),
            WasAllowed = allowed,
            WasSuccessful = wasSuccessful,
            SqlText = Trim(sqlText, 4000),
            ErrorMessage = Trim(errorMessage, 2000),
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            ResponseId = details?.ResponseId,
            PromptVersion = Trim(details?.PromptVersion, 100),
            ModelDeployment = Trim(details?.ModelDeployment, 200),
            ErrorCode = Trim(details?.ErrorCode, 100),
            VerificationStatus = Trim(details?.VerificationStatus, 50),
            DurationMs = details?.DurationMs,
            PlanningDurationMs = details?.PlanningDurationMs,
            SqlDurationMs = details?.SqlDurationMs,
            SummaryDurationMs = details?.SummaryDurationMs,
            ModelRetryCount = details?.ModelRetryCount,
            RowCount = details?.RowCount,
            WasTruncated = details?.WasTruncated,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<AiQueriesPageVm> GetAiQueriesAsync(int daysBack = 30, int latestPage = 1, int latestPageSize = 10)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var since = DateTime.UtcNow.AddDays(-daysBack);
        var baseQuery = db.AiQueryLogs!
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since);

        latestPage = Math.Max(1, latestPage);
        latestPageSize = Math.Clamp(latestPageSize, 1, 100);
        var latestTotalCount = await baseQuery.CountAsync();
        var skip = (latestPage - 1) * latestPageSize;

        var latest = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(latestPageSize)
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

        var latestVm = latest.Select(x => new AiQueryEntryVm
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            CompanyName = x.CompanyId != null && companies.TryGetValue(x.CompanyId.Value, out var name) ? name : "-",
            UserId = x.UserId,
            UserEmail = x.UserId != null && users.TryGetValue(x.UserId, out var email) ? email : x.UserId,
            Question = x.Question,
            WasSuccessful = x.WasSuccessful ?? x.WasAllowed,
            SqlText = x.SqlText,
            ErrorMessage = x.ErrorMessage,
            PromptTokens = x.PromptTokens,
            CompletionTokens = x.CompletionTokens,
            TotalTokens = x.TotalTokens,
            InputCostSek = AiTokenPricing.CalculateInputCostSek(x.PromptTokens),
            OutputCostSek = AiTokenPricing.CalculateOutputCostSek(x.CompletionTokens),
            TotalCostSek = AiTokenPricing.CalculateTotalCostSek(x.PromptTokens, x.CompletionTokens, x.TotalTokens),
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();

        var topCompanies = await baseQuery
            .GroupBy(x => x.CompanyId)
            .Select(g => new
            {
                CompanyId = g.Key,
                Count = g.Count(),
                Successful = g.Sum(x => (x.WasSuccessful ?? x.WasAllowed) ? 1 : 0)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var topCompaniesVm = topCompanies.Select(x => new AiQuerySummaryVm
        {
            CompanyId = x.CompanyId,
            CompanyName = x.CompanyId != null && companies.TryGetValue(x.CompanyId.Value, out var name) ? name : "-",
            Count = x.Count,
            Successful = x.Successful
        }).ToList();

        var totalPromptTokens = await baseQuery.Select(x => (long?)x.PromptTokens).SumAsync() ?? 0;
        var totalCompletionTokens = await baseQuery.Select(x => (long?)x.CompletionTokens).SumAsync() ?? 0;
        var totalTokens = await baseQuery.Select(x => (long?)x.TotalTokens).SumAsync() ?? 0;

        return new AiQueriesPageVm
        {
            TotalQueries = latestTotalCount,
            SuccessfulQueries = await baseQuery.CountAsync(x => (x.WasSuccessful ?? x.WasAllowed)),
            TotalTokens = totalTokens > int.MaxValue ? int.MaxValue : (int)totalTokens,
            TotalCostSek = AiTokenPricing.CalculateTotalCostSek(totalPromptTokens, totalCompletionTokens, totalTokens),
            LatestPage = latestPage,
            LatestPageSize = latestPageSize,
            LatestTotalCount = latestTotalCount,
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
