using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

// This service executes insight providers for a given audience and centralizes cache/degradation behavior.
// Customer ActionCenter and ZeeU Operations share this pipeline so provider execution stays consistent.
public interface IInsightAggregationService
{
    Task<InsightAggregationResult> GetInsightsAsync(
        UserSession user,
        ActionCenterAudience audience,
        string cacheKey,
        string logScope,
        JeevesRuntimeContext? runtimeContext,
        CancellationToken cancellationToken);

    void Invalidate(string cacheKey);
}

public sealed class InsightAggregationResult
{
    public List<ActionCenterInsight> Insights { get; init; } = new();
    public List<ActionCenterProviderFailure> Failures { get; init; } = new();
}
