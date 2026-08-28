using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application.AI.Quota;

/// <summary>
/// Defines quota states and contract for AI quota evaluation.
/// This file controls the stable API between controller/UI flow and quota logic.
/// </summary>
public enum AiQuotaStatus
{
    Disabled = 0,
    Allowed = 1,
    Warning = 2,
    NeedsDecision = 3,
    Blocked = 4,
    Paid = 5
}

public enum AiQuotaDecisionChoice
{
    AllowPaid = 0,
    BlockUntilReset = 1
}

public sealed class AiQuotaEvaluation
{
    public AiQuotaStatus Status { get; set; } = AiQuotaStatus.Disabled;
    public string Message { get; set; } = string.Empty;
    public int UsedTokens { get; set; }
    public int FreeTokens { get; set; }
    public int UsagePercent { get; set; }
    public decimal? PeriodTotalCostSek { get; set; }
    public int PaidExtraTokens { get; set; }
    public decimal? PaidExtraCostSek { get; set; }
    public bool RequiresDecision => Status == AiQuotaStatus.NeedsDecision;
    public bool IsPaidMode => Status == AiQuotaStatus.Paid;
}

public interface IAiQuotaService
{
    Task<AiQuotaEvaluation> EvaluateAsync(
        Guid? companyId,
        string? userId,
        int additionalTokens = 0,
        CancellationToken ct = default);

    Task<AiQuotaEvaluation> SetDecisionAsync(
        Guid? companyId,
        string? userId,
        AiQuotaDecisionChoice choice,
        CancellationToken ct = default);
}
