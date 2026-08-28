using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Resolves which approval step is active for a given amount.
// The engine mirrors the portal chain semantics and keeps the SQL procedure logic easy to reason about later.
public sealed class ApprovalChainRuleEngine
{
    public ApprovalChainStepViewModel? ResolveActiveStep(ApprovalChainOrderTypeViewModel orderType, decimal amount)
    {
        if (orderType is null)
            throw new ArgumentNullException(nameof(orderType));

        var orderedSteps = orderType.Steps
            .OrderBy(step => step.Sequence)
            .ToList();

        if (orderedSteps.Count == 0)
            return null;

        foreach (var step in orderedSteps)
        {
            if (ShouldStopAtStep(step, amount))
                return step;
        }

        return orderedSteps.Last();
    }

    public IReadOnlyList<ApprovalChainStepViewModel> ResolvePath(ApprovalChainOrderTypeViewModel orderType, decimal amount)
    {
        if (orderType is null)
            throw new ArgumentNullException(nameof(orderType));

        var orderedSteps = orderType.Steps
            .OrderBy(step => step.Sequence)
            .ToList();

        if (orderedSteps.Count == 0)
            return Array.Empty<ApprovalChainStepViewModel>();

        var path = new List<ApprovalChainStepViewModel>();
        foreach (var step in orderedSteps)
        {
            path.Add(step);
            if (ShouldStopAtStep(step, amount))
                break;
        }

        return path;
    }

    private static bool ShouldStopAtStep(ApprovalChainStepViewModel step, decimal amount)
    {
        if (amount >= 0)
        {
            if (step.Limit is null)
                return true;

            return amount <= step.Limit.Value;
        }

        var negativeLimit = step.NegativeLimit ?? (step.Limit.HasValue ? -step.Limit.Value : null);
        if (negativeLimit is null)
            return true;

        return amount >= negativeLimit.Value;
    }
}
