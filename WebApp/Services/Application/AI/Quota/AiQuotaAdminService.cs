using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApp.Data;
using WebApp.Models.AI;
using WebApp.Services.Application.AI;

namespace WebApp.Services.Application.AI.Quota;

/// <summary>
/// Provides portal-facing CRUD for AI quota policy (global and per-company).
/// </summary>
public sealed class AiQuotaAdminService : IAiQuotaAdminService
{
    private const string DecisionLoginProvider = "AI_QUOTA";
    private const decimal SurchargePercent = 20m;

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly AiQuotaOptions _options;

    public AiQuotaAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IOptions<AiQuotaOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _options = options.Value ?? new AiQuotaOptions();
    }

    public async Task<AiQuotaAdminSnapshot> GetSnapshotAsync(DateTime? periodStartUtc = null, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var globalEnabled = _options.Enabled;
        var globalFree = _options.FreeTokensPerPeriod;
        var globalWarn = _options.WarningThresholdPercent;

        var dbPolicies = await SafeGetPoliciesAsync(db, ct);

        var globalDb = dbPolicies.FirstOrDefault(x => x.IsGlobal);
        if (globalDb is not null)
        {
            if (globalDb.Enabled.HasValue) globalEnabled = globalDb.Enabled.Value;
            if (globalDb.FreeTokensPerPeriod.HasValue) globalFree = globalDb.FreeTokensPerPeriod.Value;
            if (globalDb.WarningThresholdPercent.HasValue) globalWarn = globalDb.WarningThresholdPercent.Value;
        }

        globalFree = Math.Max(1, globalFree);
        globalWarn = Math.Clamp(globalWarn, 1, 99);

        var companyRows = dbPolicies
            .Where(x => !x.IsGlobal && x.CompanyId.HasValue)
            .ToDictionary(x => x.CompanyId!.Value, x => x);

        var cfgRows = _options.CompanyOverrides
            .GroupBy(x => x.CompanyId)
            .ToDictionary(g => g.Key, g => g.First());

        var nowUtc = DateTime.UtcNow;
        var requestedPeriod = periodStartUtc ?? nowUtc;
        var normalizedPeriod = requestedPeriod.Kind == DateTimeKind.Utc
            ? requestedPeriod
            : DateTime.SpecifyKind(requestedPeriod, DateTimeKind.Utc);
        var periodStart = new DateTime(normalizedPeriod.Year, normalizedPeriod.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var currentPeriodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var periodStatsByCompany = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(x => x.CompanyId.HasValue && x.CreatedAtUtc >= periodStart && x.CreatedAtUtc < periodEnd)
            .GroupBy(x => x.CompanyId!.Value)
            .Select(g => new
            {
                CompanyId = g.Key,
                UsedTokens = g.Sum(x => (int?)x.TotalTokens) ?? 0,
                PromptTokens = g.Sum(x => (long?)x.PromptTokens) ?? 0,
                CompletionTokens = g.Sum(x => (long?)x.CompletionTokens) ?? 0,
                TotalTokens = g.Sum(x => (long?)x.TotalTokens) ?? 0
            })
            .ToDictionaryAsync(x => x.CompanyId, x => x, ct);

        var companies = await db.Companies!
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        var periodKey = periodStart.ToString("yyyyMM");
        var tokenNameByCompany = companies.ToDictionary(c => c.Id, c => BuildDecisionTokenName(c.Id, periodKey));
        var tokenNames = tokenNameByCompany.Values.ToList();

        var decisions = await db.Set<IdentityUserToken<string>>()
            .AsNoTracking()
            .Where(x => x.LoginProvider == DecisionLoginProvider && tokenNames.Contains(x.Name))
            .Select(x => new { x.Name, x.Value })
            .ToListAsync(ct);

        var decisionStats = decisions
            .GroupBy(x => x.Name)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Paid = g.Count(x => string.Equals(x.Value, "allow_paid", StringComparison.OrdinalIgnoreCase)),
                    Blocked = g.Count(x => string.Equals(x.Value, "block_until_reset", StringComparison.OrdinalIgnoreCase))
                });

        var resultCompanies = companies.Select(c =>
        {
            cfgRows.TryGetValue(c.Id, out var cfg);
            companyRows.TryGetValue(c.Id, out var dbOverride);

            var effectiveEnabled = dbOverride?.Enabled ?? globalEnabled;
            var effectiveFree = dbOverride?.FreeTokensPerPeriod
                ?? cfg?.FreeTokensPerPeriod
                ?? globalFree;
            var effectiveWarn = dbOverride?.WarningThresholdPercent
                ?? cfg?.WarningThresholdPercent
                ?? globalWarn;

            var hasPeriodStats = periodStatsByCompany.TryGetValue(c.Id, out var periodStats);
            var used = hasPeriodStats ? periodStats!.UsedTokens : 0;
            effectiveFree = Math.Max(1, effectiveFree);
            effectiveWarn = Math.Clamp(effectiveWarn, 1, 99);

            var tokenName = tokenNameByCompany[c.Id];
            decisionStats.TryGetValue(tokenName, out var decisionStatsForCompany);
            var paidUsers = decisionStatsForCompany?.Paid ?? 0;
            var blockedUsers = decisionStatsForCompany?.Blocked ?? 0;
            var mode = paidUsers > 0 ? "paid" : blockedUsers > 0 ? "blocked" : "standard";

            var totalCostSek = hasPeriodStats
                ? (AiTokenPricing.CalculateTotalCostSek(
                    promptTokens: periodStats!.PromptTokens,
                    completionTokens: periodStats.CompletionTokens,
                    totalTokens: periodStats.TotalTokens) ?? 0m)
                : 0m;
            var paidExtraTokens = Math.Max(0, used - effectiveFree);
            var paidExtraBaseCost = CalculatePaidExtraCost(totalCostSek, used, paidExtraTokens);
            var paidExtraRevenue = RoundSek(paidExtraBaseCost * (SurchargePercent / 100m));
            var paidExtraBillable = RoundSek(paidExtraBaseCost + paidExtraRevenue);

            return new AiQuotaCompanySnapshot
            {
                CompanyId = c.Id,
                CompanyName = string.IsNullOrWhiteSpace(c.Name) ? "-" : c.Name!,
                HasOverride = dbOverride is not null,
                EnabledOverride = dbOverride?.Enabled,
                FreeTokensPerPeriodOverride = dbOverride?.FreeTokensPerPeriod,
                WarningThresholdPercentOverride = dbOverride?.WarningThresholdPercent,
                EffectiveEnabled = effectiveEnabled,
                EffectiveFreeTokensPerPeriod = effectiveFree,
                EffectiveWarningThresholdPercent = effectiveWarn,
                UsedTokensCurrentPeriod = used,
                UsagePercentCurrentPeriod = Math.Clamp((int)Math.Round((used / (double)effectiveFree) * 100d), 0, 999),
                CurrentPeriodMode = mode,
                PaidUsersCount = paidUsers,
                BlockedUsersCount = blockedUsers,
                PaidExtraTokensCurrentPeriod = paidExtraTokens,
                PaidExtraBaseCostSekCurrentPeriod = paidExtraBaseCost,
                PaidExtraRevenueSekCurrentPeriod = paidExtraRevenue,
                PaidExtraBillableSekCurrentPeriod = paidExtraBillable
            };
        }).ToList();

        return new AiQuotaAdminSnapshot
        {
            PeriodYear = periodStart.Year,
            PeriodMonth = periodStart.Month,
            PeriodStartUtc = periodStart,
            IsHistoricalPeriod = periodStart < currentPeriodStart,
            GlobalEnabled = globalEnabled,
            GlobalFreeTokensPerPeriod = globalFree,
            GlobalWarningThresholdPercent = globalWarn,
            SurchargePercent = SurchargePercent,
            TotalPaidExtraTokensCurrentPeriod = resultCompanies.Sum(x => x.PaidExtraTokensCurrentPeriod),
            TotalPaidExtraBaseCostSekCurrentPeriod = RoundSek(resultCompanies.Sum(x => x.PaidExtraBaseCostSekCurrentPeriod)),
            TotalPaidExtraRevenueSekCurrentPeriod = RoundSek(resultCompanies.Sum(x => x.PaidExtraRevenueSekCurrentPeriod)),
            TotalPaidExtraBillableSekCurrentPeriod = RoundSek(resultCompanies.Sum(x => x.PaidExtraBillableSekCurrentPeriod)),
            Companies = resultCompanies
        };
    }

    public async Task SaveGlobalPolicyAsync(AiQuotaGlobalPolicyInput input, string? updatedByUserId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var set = db.AiQuotaPolicies!;
            var row = await set.FirstOrDefaultAsync(x => x.IsGlobal, ct);
            if (row is null)
            {
                row = new AiQuotaPolicy
                {
                    Id = Guid.NewGuid(),
                    IsGlobal = true
                };
                await set.AddAsync(row, ct);
            }

            row.Enabled = input.Enabled;
            row.FreeTokensPerPeriod = Math.Max(1, input.FreeTokensPerPeriod);
            row.WarningThresholdPercent = Math.Clamp(input.WarningThresholdPercent, 1, 99);
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = updatedByUserId;

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsMissingQuotaPolicyTable(ex))
        {
            throw new InvalidOperationException("Tabellen för AI-kvotpolicy saknas. Kör migrationen AddAiQuotaPolicies först.", ex);
        }
    }

    public async Task SaveCompanyOverrideAsync(AiQuotaCompanyPolicyInput input, string? updatedByUserId, CancellationToken ct = default)
    {
        var hasAnyOverride =
            input.EnabledOverrideSet ||
            input.FreeTokensPerPeriodOverride.HasValue ||
            input.WarningThresholdPercentOverride.HasValue;

        if (!hasAnyOverride)
        {
            await RemoveCompanyOverrideAsync(input.CompanyId, ct);
            return;
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var set = db.AiQuotaPolicies!;
            var row = await set.FirstOrDefaultAsync(x => !x.IsGlobal && x.CompanyId == input.CompanyId, ct);
            if (row is null)
            {
                row = new AiQuotaPolicy
                {
                    Id = Guid.NewGuid(),
                    IsGlobal = false,
                    CompanyId = input.CompanyId
                };
                await set.AddAsync(row, ct);
            }

            row.Enabled = input.EnabledOverrideSet ? input.EnabledOverride : null;
            row.FreeTokensPerPeriod = input.FreeTokensPerPeriodOverride.HasValue
                ? Math.Max(1, input.FreeTokensPerPeriodOverride.Value)
                : null;
            row.WarningThresholdPercent = input.WarningThresholdPercentOverride.HasValue
                ? Math.Clamp(input.WarningThresholdPercentOverride.Value, 1, 99)
                : null;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = updatedByUserId;

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsMissingQuotaPolicyTable(ex))
        {
            throw new InvalidOperationException("Tabellen för AI-kvotpolicy saknas. Kör migrationen AddAiQuotaPolicies först.", ex);
        }
    }

    public async Task RemoveCompanyOverrideAsync(Guid companyId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var row = await db.AiQuotaPolicies!
                .FirstOrDefaultAsync(x => !x.IsGlobal && x.CompanyId == companyId, ct);
            if (row is null) return;

            db.AiQuotaPolicies!.Remove(row);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsMissingQuotaPolicyTable(ex))
        {
            throw new InvalidOperationException("Tabellen för AI-kvotpolicy saknas. Kör migrationen AddAiQuotaPolicies först.", ex);
        }
    }

    public async Task ResetCompanyCurrentPeriodModeAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var nowUtc = DateTime.UtcNow;
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodKey = periodStart.ToString("yyyyMM");
        var tokenName = BuildDecisionTokenName(companyId, periodKey);

        var set = db.Set<IdentityUserToken<string>>();
        var rows = await set
            .Where(x => x.LoginProvider == DecisionLoginProvider && x.Name == tokenName)
            .ToListAsync(ct);
        if (!rows.Any())
            return;

        set.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<AiQuotaPolicy>> SafeGetPoliciesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        try
        {
            return await db.AiQuotaPolicies!
                .AsNoTracking()
                .ToListAsync(ct);
        }
        catch (Exception ex) when (IsMissingQuotaPolicyTable(ex))
        {
            return new List<AiQuotaPolicy>();
        }
    }

    private static bool IsMissingQuotaPolicyTable(Exception ex)
    {
        if (ex is SqlException sqlEx)
            return sqlEx.Message.Contains("AiQuotaPolicies", StringComparison.OrdinalIgnoreCase);

        return ex.Message.Contains("AiQuotaPolicies", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException is not null && IsMissingQuotaPolicyTable(ex.InnerException));
    }

    private static string BuildDecisionTokenName(Guid companyId, string periodKey)
        => $"ai-quota:{companyId:N}:{periodKey}";

    private static decimal CalculatePaidExtraCost(decimal totalCostSek, int usedTokens, int paidExtraTokens)
    {
        if (totalCostSek <= 0m || usedTokens <= 0 || paidExtraTokens <= 0)
            return 0m;

        var ratio = paidExtraTokens / (decimal)usedTokens;
        return RoundSek(totalCostSek * ratio);
    }

    private static decimal RoundSek(decimal amount)
        => Math.Round(amount, 4, MidpointRounding.AwayFromZero);
}
