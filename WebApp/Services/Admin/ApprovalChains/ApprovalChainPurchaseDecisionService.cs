using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Dry-runs purchase approval decisions from portal-owned rules without updating Jeeves.
public sealed class ApprovalChainPurchaseDecisionService : IApprovalChainPurchaseDecisionService
{
    private readonly ApplicationDbContext _context;

    public ApprovalChainPurchaseDecisionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApprovalChainPurchaseDecision> EvaluateAsync(
        ApprovalChainPurchaseDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = await _context.ApprovalChainRules!
            .AsNoTracking()
            .Where(rule => rule.ForetagKod == request.CompanyCode
                && rule.FlowId == request.FlowId
                && rule.PurchaseOrderType == request.PurchaseOrderType)
            .OrderBy(rule => rule.SqlIdentity)
            .ToListAsync(cancellationToken);

        var rule = rules.FirstOrDefault(rule =>
            string.Equals(rule.CurrentApproverPersSign, request.CurrentApproverPersSign, StringComparison.OrdinalIgnoreCase))
            ?? rules.FirstOrDefault(rule => rule.IsDefault);

        if (rule is null)
        {
            return new ApprovalChainPurchaseDecision(
                ApprovalChainDecisionKind.NoRule,
                null,
                null,
                null,
                null,
                false,
                false,
                "No approval chain rule matched the purchase order.");
        }

        var orderValue = Math.Abs(request.OrderValue);
        var shouldForward = ShouldForward(orderValue, request.CurrentApproverPersSign, rule);
        var decisionKind = shouldForward
            ? ApprovalChainDecisionKind.ForwardToNextApprover
            : ApprovalChainDecisionKind.FinalApprove;

        return new ApprovalChainPurchaseDecision(
            decisionKind,
            rule.NextApproverPersSign,
            rule.Limit,
            rule.NegativeLimit,
            rule.SqlIdentity,
            rule.IsDefault,
            rule.SendMail,
            shouldForward
                ? "Order value exceeds the current approval rule and should be sent to the next approver."
                : "Order can be final-approved by the current approver.");
    }

    private static bool ShouldForward(decimal absoluteOrderValue, string currentApproverPersSign, ApprovalChainRuleRecord rule)
    {
        if (string.Equals(rule.NextApproverPersSign, currentApproverPersSign, StringComparison.OrdinalIgnoreCase))
            return false;

        return rule.Limit != 0 && rule.Limit <= absoluteOrderValue;
    }
}

public interface IApprovalChainPurchaseDecisionService
{
    Task<ApprovalChainPurchaseDecision> EvaluateAsync(
        ApprovalChainPurchaseDecisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalChainPurchaseDecisionRequest(
    short CompanyCode,
    int FlowId,
    string CurrentApproverPersSign,
    short PurchaseOrderType,
    decimal OrderValue);

public sealed record ApprovalChainPurchaseDecision(
    ApprovalChainDecisionKind Kind,
    string? NextApproverPersSign,
    decimal? Limit,
    decimal? NegativeLimit,
    int? RuleSqlIdentity,
    bool UsedDefaultRule,
    bool SendMail,
    string Message);

public enum ApprovalChainDecisionKind
{
    ForwardToNextApprover,
    FinalApprove,
    NoRule
}
