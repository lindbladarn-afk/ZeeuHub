using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApp.Data;
using WebApp.Services.Application.AI;

namespace WebApp.Services.Application.AI.Quota;

/// <summary>
/// Evaluates AI quota state and persists user overage decisions per period.
/// This class controls when AI is allowed, warned, blocked, or switched to paid mode.
/// </summary>
public sealed class AiQuotaService : IAiQuotaService
{
    private const string DecisionLoginProvider = "AI_QUOTA";

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly AiQuotaOptions _options;

    public AiQuotaService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IOptions<AiQuotaOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _options = options.Value ?? new AiQuotaOptions();
    }

    public async Task<AiQuotaEvaluation> EvaluateAsync(
        Guid? companyId,
        string? userId,
        int additionalTokens = 0,
        CancellationToken ct = default)
    {
        if (companyId is null || string.IsNullOrWhiteSpace(userId))
            return Disabled();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var policy = await ResolvePolicyAsync(db, companyId.Value, ct);
        if (!policy.Enabled)
            return Disabled();

        var nowUtc = DateTime.UtcNow;
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var periodKey = periodStart.ToString("yyyyMM");

        var usedTokens = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.CreatedAtUtc >= periodStart &&
                x.CreatedAtUtc < periodEnd)
            .Select(x => (int?)x.TotalTokens)
            .SumAsync(ct) ?? 0;

        var periodPromptTokens = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.CreatedAtUtc >= periodStart &&
                x.CreatedAtUtc < periodEnd)
            .Select(x => (long?)x.PromptTokens)
            .SumAsync(ct) ?? 0;

        var periodCompletionTokens = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.CreatedAtUtc >= periodStart &&
                x.CreatedAtUtc < periodEnd)
            .Select(x => (long?)x.CompletionTokens)
            .SumAsync(ct) ?? 0;

        var periodTotalTokens = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.CreatedAtUtc >= periodStart &&
                x.CreatedAtUtc < periodEnd)
            .Select(x => (long?)x.TotalTokens)
            .SumAsync(ct) ?? 0;

        var periodTotalCostSek = AiTokenPricing.CalculateTotalCostSek(
            promptTokens: periodPromptTokens,
            completionTokens: periodCompletionTokens,
            totalTokens: periodTotalTokens);

        if (additionalTokens > 0)
            usedTokens += additionalTokens;

        var freeTokens = Math.Max(1, policy.FreeTokensPerPeriod);
        var usagePercent = Math.Clamp((int)Math.Round((usedTokens / (double)freeTokens) * 100d), 0, 999);
        var paidExtraTokens = Math.Max(0, usedTokens - freeTokens);
        var paidExtraCostSek = CalculatePaidExtraCost(periodTotalCostSek, usedTokens, paidExtraTokens);

        if (usedTokens < freeTokens)
        {
            var warnAt = Math.Clamp(policy.WarningThresholdPercent, 1, 99);
            if (usagePercent >= warnAt)
            {
                return new AiQuotaEvaluation
                {
                    Status = AiQuotaStatus.Warning,
                    Message = $"Du har använt {usagePercent}% av din fria AI-kvot ({usedTokens}/{freeTokens} tokens).",
                    UsedTokens = usedTokens,
                    FreeTokens = freeTokens,
                    UsagePercent = usagePercent,
                    PeriodTotalCostSek = periodTotalCostSek,
                    PaidExtraTokens = paidExtraTokens,
                    PaidExtraCostSek = paidExtraCostSek
                };
            }

            return new AiQuotaEvaluation
            {
                Status = AiQuotaStatus.Allowed,
                Message = string.Empty,
                UsedTokens = usedTokens,
                FreeTokens = freeTokens,
                UsagePercent = usagePercent,
                PeriodTotalCostSek = periodTotalCostSek,
                PaidExtraTokens = paidExtraTokens,
                PaidExtraCostSek = paidExtraCostSek
            };
        }

        var decision = await GetDecisionAsync(db, userId!, companyId.Value, periodKey, ct);
        if (string.Equals(decision, "allow_paid", StringComparison.OrdinalIgnoreCase))
        {
            return new AiQuotaEvaluation
            {
                Status = AiQuotaStatus.Paid,
                Message = "Du har förbrukat fria tokens. Frågor fortsätter i betal-läge resten av perioden. Vid nästa periodstart återställs fria tokens.",
                UsedTokens = usedTokens,
                FreeTokens = freeTokens,
                UsagePercent = usagePercent,
                PeriodTotalCostSek = periodTotalCostSek,
                PaidExtraTokens = paidExtraTokens,
                PaidExtraCostSek = paidExtraCostSek
            };
        }

        if (string.Equals(decision, "block_until_reset", StringComparison.OrdinalIgnoreCase))
        {
            return new AiQuotaEvaluation
            {
                Status = AiQuotaStatus.Blocked,
                Message = "Din fria AI-kvot är slut. AI är pausad tills nästa periodstart.",
                UsedTokens = usedTokens,
                FreeTokens = freeTokens,
                UsagePercent = usagePercent,
                PeriodTotalCostSek = periodTotalCostSek,
                PaidExtraTokens = paidExtraTokens,
                PaidExtraCostSek = paidExtraCostSek
            };
        }

        return new AiQuotaEvaluation
        {
            Status = AiQuotaStatus.NeedsDecision,
            Message = "Din fria AI-kvot är slut. Välj om du vill fortsätta med kostnad resten av perioden eller pausa tills nästa periodstart.",
            UsedTokens = usedTokens,
            FreeTokens = freeTokens,
            UsagePercent = usagePercent,
            PeriodTotalCostSek = periodTotalCostSek,
            PaidExtraTokens = paidExtraTokens,
            PaidExtraCostSek = paidExtraCostSek
        };
    }

    public async Task<AiQuotaEvaluation> SetDecisionAsync(
        Guid? companyId,
        string? userId,
        AiQuotaDecisionChoice choice,
        CancellationToken ct = default)
    {
        if (companyId is null || string.IsNullOrWhiteSpace(userId))
            return Disabled();

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var policy = await ResolvePolicyAsync(db, companyId.Value, ct);
        if (!policy.Enabled)
            return Disabled();

        var nowUtc = DateTime.UtcNow;
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodKey = periodStart.ToString("yyyyMM");
        var tokenName = BuildDecisionTokenName(companyId.Value, periodKey);
        var tokenValue = choice == AiQuotaDecisionChoice.AllowPaid ? "allow_paid" : "block_until_reset";

        var set = db.Set<IdentityUserToken<string>>();
        var existing = await set.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.LoginProvider == DecisionLoginProvider &&
            x.Name == tokenName, ct);

        if (existing is null)
        {
            await set.AddAsync(new IdentityUserToken<string>
            {
                UserId = userId!,
                LoginProvider = DecisionLoginProvider,
                Name = tokenName,
                Value = tokenValue
            }, ct);
        }
        else
        {
            existing.Value = tokenValue;
        }

        await db.SaveChangesAsync(ct);
        return await EvaluateAsync(companyId, userId, additionalTokens: 0, ct);
    }

    private async Task<(bool Enabled, int FreeTokensPerPeriod, int WarningThresholdPercent)> ResolvePolicyAsync(ApplicationDbContext db, Guid companyId, CancellationToken ct)
    {
        var enabled = _options.Enabled;
        var free = _options.FreeTokensPerPeriod;
        var warning = _options.WarningThresholdPercent;

        var cfgOv = _options.CompanyOverrides
            .FirstOrDefault(x => x.CompanyId == companyId);

        if (cfgOv is not null)
        {
            if (cfgOv.FreeTokensPerPeriod.HasValue)
                free = cfgOv.FreeTokensPerPeriod.Value;
            if (cfgOv.WarningThresholdPercent.HasValue)
                warning = cfgOv.WarningThresholdPercent.Value;
        }

        try
        {
            var globalDb = await db.AiQuotaPolicies!
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsGlobal, ct);
            if (globalDb is not null)
            {
                if (globalDb.Enabled.HasValue)
                    enabled = globalDb.Enabled.Value;
                if (globalDb.FreeTokensPerPeriod.HasValue)
                    free = globalDb.FreeTokensPerPeriod.Value;
                if (globalDb.WarningThresholdPercent.HasValue)
                    warning = globalDb.WarningThresholdPercent.Value;
            }

            var companyDb = await db.AiQuotaPolicies!
                .AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsGlobal && x.CompanyId == companyId, ct);
            if (companyDb is not null)
            {
                if (companyDb.Enabled.HasValue)
                    enabled = companyDb.Enabled.Value;
                if (companyDb.FreeTokensPerPeriod.HasValue)
                    free = companyDb.FreeTokensPerPeriod.Value;
                if (companyDb.WarningThresholdPercent.HasValue)
                    warning = companyDb.WarningThresholdPercent.Value;
            }
        }
        catch (Exception ex) when (IsMissingQuotaPolicyTable(ex))
        {
            // Backward compatibility when migration has not been applied yet.
        }

        return (enabled, Math.Max(1, free), Math.Clamp(warning, 1, 99));
    }

    private async Task<string?> GetDecisionAsync(
        ApplicationDbContext db,
        string userId,
        Guid companyId,
        string periodKey,
        CancellationToken ct)
    {
        var tokenName = BuildDecisionTokenName(companyId, periodKey);
        return await db.Set<IdentityUserToken<string>>()
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.LoginProvider == DecisionLoginProvider &&
                x.Name == tokenName)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
    }

    private static string BuildDecisionTokenName(Guid companyId, string periodKey)
        => $"ai-quota:{companyId:N}:{periodKey}";

    private static bool IsMissingQuotaPolicyTable(Exception ex)
    {
        if (ex is SqlException sqlEx)
            return sqlEx.Message.Contains("AiQuotaPolicies", StringComparison.OrdinalIgnoreCase);

        return ex.Message.Contains("AiQuotaPolicies", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException is not null && IsMissingQuotaPolicyTable(ex.InnerException));
    }

    private static AiQuotaEvaluation Disabled() => new()
    {
        Status = AiQuotaStatus.Disabled,
        Message = string.Empty
    };

    private static decimal? CalculatePaidExtraCost(decimal? periodTotalCostSek, int usedTokens, int paidExtraTokens)
    {
        if (!periodTotalCostSek.HasValue || periodTotalCostSek.Value < 0 || usedTokens <= 0 || paidExtraTokens <= 0)
            return 0m;

        var ratio = paidExtraTokens / (decimal)usedTokens;
        return Math.Round(periodTotalCostSek.Value * ratio, 4, MidpointRounding.AwayFromZero);
    }
}
