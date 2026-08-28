using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;

namespace WebApp.Services.Operations;

// This service aggregates admin-only operational insights for ZeeU.
// It intentionally ignores customer item-state history until a dedicated internal workflow/state model is introduced.
public sealed class ZeeuOperationsService : IZeeuOperationsService
{
    private readonly IInsightAggregationService _aggregationService;

    public ZeeuOperationsService(
        IInsightAggregationService aggregationService)
    {
        _aggregationService = aggregationService;
    }

    public void InvalidateCache(UserSession user)
    {
        _aggregationService.Invalidate(BuildCacheKey(user));
    }

    public async Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken)
    {
        var (insights, failures) = await GetInsightsInternalAsync(user, cancellationToken);
        var limited = insights
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.DetectedAt)
            .Take(Math.Max(0, take))
            .ToList();

        return new ActionCenterViewModel
        {
            TotalCount = insights.Count,
            Audience = ActionCenterAudience.InternalAdmin,
            IsDegraded = failures.Count > 0,
            Insights = limited,
            History = Array.Empty<ActionCenterHistoryItem>(),
            ProviderFailures = failures
        };
    }

    public async Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken)
    {
        var (insights, failures) = await GetInsightsInternalAsync(user, cancellationToken);
        return new ActionCenterSummaryDto
        {
            Count = insights.Count,
            HasHighPriority = insights.Any(x => x.Priority == ActionCenterPriority.High),
            IsDegraded = failures.Count > 0,
            Audience = ActionCenterAudience.InternalAdmin,
            LatestDetectedAt = insights.Count == 0 ? null : insights.Max(x => x.DetectedAt)
        };
    }

    private Task<(List<ActionCenterInsight> insights, List<ActionCenterProviderFailure> failures)> GetInsightsInternalAsync(UserSession user, CancellationToken cancellationToken)
        => BuildInsightsAsync(user, cancellationToken);

    private static string BuildCacheKey(UserSession user) =>
        $"action-center:{ActionCenterAudience.InternalAdmin}:{user.UserId}:{user.CompanyId}:{user.JeevesActiveCompany}";

    private async Task<(List<ActionCenterInsight> insights, List<ActionCenterProviderFailure> failures)> BuildInsightsAsync(UserSession user, CancellationToken cancellationToken)
    {
        var aggregation = await _aggregationService.GetInsightsAsync(
            user,
            ActionCenterAudience.InternalAdmin,
            BuildCacheKey(user),
            "ZeeuOperations",
            runtimeContext: null,
            cancellationToken);

        return (aggregation.Insights, aggregation.Failures);
    }
}
