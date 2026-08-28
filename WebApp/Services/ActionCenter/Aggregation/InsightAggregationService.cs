using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

// This shared engine runs insight providers, caches their results, and converts exceptions into provider failures.
// It keeps the customer and internal admin pipelines structurally identical while still allowing different audiences.
public sealed class InsightAggregationService : IInsightAggregationService
{
    private const string GenericProviderFailureMessage = "Insikten kunde inte laddas just nu.";

    private readonly IMemoryCache _cache;
    private readonly IEnumerable<IInsightProvider> _providers;
    private readonly ILogger<InsightAggregationService> _logger;

    public InsightAggregationService(
        IMemoryCache cache,
        IEnumerable<IInsightProvider> providers,
        ILogger<InsightAggregationService> logger)
    {
        _cache = cache;
        _providers = providers;
        _logger = logger;
    }

    public void Invalidate(string cacheKey)
    {
        _cache.Remove(cacheKey);
    }

    public Task<InsightAggregationResult> GetInsightsAsync(
        UserSession user,
        ActionCenterAudience audience,
        string cacheKey,
        string logScope,
        JeevesRuntimeContext? runtimeContext,
        CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await BuildAsync(user, audience, logScope, runtimeContext, cancellationToken);
        })!;
    }

    private async Task<InsightAggregationResult> BuildAsync(
        UserSession user,
        ActionCenterAudience audience,
        string logScope,
        JeevesRuntimeContext? runtimeContext,
        CancellationToken cancellationToken)
    {
        var insights = new List<ActionCenterInsight>();
        var failures = new List<ActionCenterProviderFailure>();

        foreach (var provider in _providers.Where(x => x.Audience == audience))
        {
            try
            {
                var providerInsights = await provider.GetInsightsAsync(user, runtimeContext, cancellationToken);
                if (providerInsights != null)
                {
                    insights.AddRange(providerInsights);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "{LogScope} provider {ProviderKey} timed out or was cancelled for user {UserId}",
                    logScope,
                    provider.ProviderKey,
                    user.UserId);

                failures.Add(new ActionCenterProviderFailure
                {
                    ProviderKey = provider.ProviderKey,
                    Audience = provider.Audience,
                    Message = "Laddningen hann inte slutföras i tid.",
                    OccurredAtUtc = DateTime.UtcNow
                });

                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{LogScope} provider {ProviderKey} failed for user {UserId}",
                    logScope,
                    provider.ProviderKey,
                    user.UserId);

                failures.Add(new ActionCenterProviderFailure
                {
                    ProviderKey = provider.ProviderKey,
                    Audience = provider.Audience,
                    Message = GenericProviderFailureMessage,
                    OccurredAtUtc = DateTime.UtcNow
                });
            }
        }

        return new InsightAggregationResult
        {
            Insights = insights,
            Failures = failures
        };
    }
}
