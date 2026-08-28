using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application.AI.Quota;

/// <summary>
/// Contracts for portal-admin management of AI quota policies.
/// </summary>
public sealed class AiQuotaAdminSnapshot
{
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public bool IsHistoricalPeriod { get; set; }
    public bool GlobalEnabled { get; set; }
    public int GlobalFreeTokensPerPeriod { get; set; }
    public int GlobalWarningThresholdPercent { get; set; }
    public decimal SurchargePercent { get; set; }
    public int TotalPaidExtraTokensCurrentPeriod { get; set; }
    public decimal TotalPaidExtraBaseCostSekCurrentPeriod { get; set; }
    public decimal TotalPaidExtraRevenueSekCurrentPeriod { get; set; }
    public decimal TotalPaidExtraBillableSekCurrentPeriod { get; set; }
    public IReadOnlyCollection<AiQuotaCompanySnapshot> Companies { get; set; } = Array.Empty<AiQuotaCompanySnapshot>();
}

public sealed class AiQuotaCompanySnapshot
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = "-";
    public bool HasOverride { get; set; }
    public bool? EnabledOverride { get; set; }
    public int? FreeTokensPerPeriodOverride { get; set; }
    public int? WarningThresholdPercentOverride { get; set; }
    public bool EffectiveEnabled { get; set; }
    public int EffectiveFreeTokensPerPeriod { get; set; }
    public int EffectiveWarningThresholdPercent { get; set; }
    public int UsedTokensCurrentPeriod { get; set; }
    public int UsagePercentCurrentPeriod { get; set; }
    public string CurrentPeriodMode { get; set; } = "standard";
    public int PaidUsersCount { get; set; }
    public int BlockedUsersCount { get; set; }
    public int PaidExtraTokensCurrentPeriod { get; set; }
    public decimal PaidExtraBaseCostSekCurrentPeriod { get; set; }
    public decimal PaidExtraRevenueSekCurrentPeriod { get; set; }
    public decimal PaidExtraBillableSekCurrentPeriod { get; set; }
}

public sealed class AiQuotaGlobalPolicyInput
{
    public bool Enabled { get; set; }
    public int FreeTokensPerPeriod { get; set; }
    public int WarningThresholdPercent { get; set; }
}

public sealed class AiQuotaCompanyPolicyInput
{
    public Guid CompanyId { get; set; }
    public bool EnabledOverrideSet { get; set; }
    public bool? EnabledOverride { get; set; }
    public int? FreeTokensPerPeriodOverride { get; set; }
    public int? WarningThresholdPercentOverride { get; set; }
}

public interface IAiQuotaAdminService
{
    Task<AiQuotaAdminSnapshot> GetSnapshotAsync(DateTime? periodStartUtc = null, CancellationToken ct = default);
    Task SaveGlobalPolicyAsync(AiQuotaGlobalPolicyInput input, string? updatedByUserId, CancellationToken ct = default);
    Task SaveCompanyOverrideAsync(AiQuotaCompanyPolicyInput input, string? updatedByUserId, CancellationToken ct = default);
    Task RemoveCompanyOverrideAsync(Guid companyId, CancellationToken ct = default);
    Task ResetCompanyCurrentPeriodModeAsync(Guid companyId, CancellationToken ct = default);
}
