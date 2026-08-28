using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.ActionCenter;

public sealed class ActionCenterService : IActionCenterService
{
    private readonly IInsightAggregationService _aggregationService;
    private readonly IActionCenterStateStore _stateStore;
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;

    public ActionCenterService(
        IInsightAggregationService aggregationService,
        IActionCenterStateStore stateStore,
        IJeevesRuntimeContextService jeevesRuntimeContextService)
    {
        _aggregationService = aggregationService;
        _stateStore = stateStore;
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
    }

    public void InvalidateCache(UserSession user)
    {
        _aggregationService.Invalidate(BuildCacheKey(user));
    }

    public async Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken)
    {
        var (insights, history, failures, availabilityBanner) = await GetInsightsInternalAsync(user, cancellationToken);
        var limited = insights
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.DetectedAt)
            .Take(Math.Max(0, take))
            .ToList();

        return new ActionCenterViewModel
        {
            TotalCount = insights.Count,
            Audience = ActionCenterAudience.Customer,
            IsDegraded = failures.Count > 0,
            AvailabilityBanner = availabilityBanner,
            Insights = limited,
            History = history,
            ProviderFailures = failures
        };
    }

    public async Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken)
    {
        var (insights, _, failures, _) = await GetInsightsInternalAsync(user, cancellationToken);
        return new ActionCenterSummaryDto
        {
            Count = insights.Count,
            HasHighPriority = insights.Any(x => x.Priority == ActionCenterPriority.High),
            IsDegraded = failures.Count > 0,
            Audience = ActionCenterAudience.Customer,
            LatestDetectedAt = insights.Count == 0 ? null : insights.Max(x => x.DetectedAt)
        };
    }

    private Task<(List<ActionCenterInsight> insights, List<ActionCenterHistoryItem> history, List<ActionCenterProviderFailure> failures, ModuleBannerViewModel? availabilityBanner)> GetInsightsInternalAsync(UserSession user, CancellationToken cancellationToken)
        => BuildInsightsAsync(user, cancellationToken);

    private static string BuildCacheKey(UserSession user) =>
        $"action-center:{ActionCenterAudience.Customer}:{user.UserId}:{user.JeevesActiveCompany}:{user.PersSign}";

    private async Task<(List<ActionCenterInsight> insights, List<ActionCenterHistoryItem> history, List<ActionCenterProviderFailure> failures, ModuleBannerViewModel? availabilityBanner)> BuildInsightsAsync(UserSession user, CancellationToken cancellationToken)
    {
        var runtimeContextResult = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        var runtimeContext = runtimeContextResult.Success ? runtimeContextResult.Value : null;
        var runtimeUser = BuildRuntimeUser(user, runtimeContext);
        var aggregation = await _aggregationService.GetInsightsAsync(
            runtimeUser,
            ActionCenterAudience.Customer,
            BuildCacheKey(runtimeUser),
            "ActionCenter",
            runtimeContext,
            cancellationToken);

        var insights = aggregation.Insights;
        ModuleBannerViewModel? availabilityBanner = null;

        if (runtimeContext is null)
        {
            insights = insights.Where(x => !x.IsMock).ToList();
            availabilityBanner = BuildRuntimeUnavailableBanner(runtimeUser, runtimeContextResult.Error);
        }

        if (insights.Any(x => !x.IsMock))
        {
            // Once real tenant signals exist we hide demo cards so the Action Center reads as operational, not mocked.
            insights = insights.Where(x => !x.IsMock).ToList();
        }
        var failures = aggregation.Failures;

        // Slå ihop med användarens sparade status
        var states = await _stateStore.GetStatesAsync(runtimeUser.CompanyId, runtimeUser.UserId, cancellationToken);
        var history = new List<ActionCenterHistoryItem>();
        if (states.Count == 0)
            return (insights, history, failures, availabilityBanner);

        var stateDict = states.GroupBy(s => s.ExternalId)
                              .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAtUtc).First());

        var filtered = new List<ActionCenterInsight>();
        foreach (var item in insights)
        {
            if (item.IsMock)
            {
                filtered.Add(item);
                continue;
            }

            if (stateDict.TryGetValue(item.Key, out var state))
            {
                if (state.Status == ActionCenterItemStatus.Completed || state.Status == ActionCenterItemStatus.Dismissed)
                {
                    history.Add(ToHistory(state));
                    continue; // filter bort klara/dismissade
                }
                if (state.Status == ActionCenterItemStatus.Active)
                {
                    item.Status = ActionCenterStatus.InProgress;
                }
            }
            filtered.Add(item);
        }

        foreach (var state in states.Where(x => x.Status == ActionCenterItemStatus.Completed || x.Status == ActionCenterItemStatus.Dismissed))
        {
            if (!history.Any(h => h.Key == state.ExternalId))
            {
                history.Add(ToHistory(state));
            }
        }

        history = history.OrderByDescending(h => h.CompletedAt ?? h.DetectedAt).Take(20).ToList();

        return (filtered, history, failures, availabilityBanner);
    }

    private static UserSession BuildRuntimeUser(UserSession user, JeevesRuntimeContext? runtimeContext)
    {
        if (runtimeContext is null)
        {
            return new UserSession
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Language = user.Language,
                CompanyName = user.CompanyName,
                PersSign = user.PersSign,
                CompanyId = user.CompanyId,
                JeevesActiveCompany = user.JeevesActiveCompany
            };
        }

        return new UserSession
        {
            UserId = user.UserId,
            Email = runtimeContext.Email ?? user.Email,
            FirstName = runtimeContext.FirstName ?? user.FirstName,
            LastName = runtimeContext.LastName ?? user.LastName,
            Language = user.Language,
            CompanyName = runtimeContext.CompanyName,
            PersSign = runtimeContext.PersSign ?? user.PersSign,
            CompanyId = runtimeContext.CompanyId,
            JeevesActiveCompany = runtimeContext.CompanyCode
        };
    }

    private static ActionCenterHistoryItem ToHistory(ActionCenterItemState state)
    {
        return new ActionCenterHistoryItem
        {
            Key = state.ExternalId,
            Audience = ActionCenterAudience.Customer,
            Title = state.Title ?? state.ExternalId,
            Description = state.Description ?? string.Empty,
            Category = state.Category ?? "Historik",
            Priority = state.Priority ?? ActionCenterPriority.Info,
            DetectedAt = state.DetectedAtUtc ?? DateTime.UtcNow,
            CompletedAt = state.CompletedAtUtc,
            Comment = state.Comment
        };
    }

    private static ModuleBannerViewModel BuildRuntimeUnavailableBanner(UserSession user, string? detail)
    {
        var companyName = string.IsNullOrWhiteSpace(user.CompanyName) ? "valt bolag" : user.CompanyName;

        return new ModuleBannerViewModel
        {
            Title = "Vissa insikter kunde inte laddas",
            Message = $"ZeeU Action Center fungerar fortfarande, men insikter som kräver live-data från Jeeves kunde inte laddas för {companyName}.",
            Note = NormalizeRuntimeDetail(detail),
            Tone = "warning",
            IconClass = "fa fa-plug",
            Compact = false
        };
    }

    private static string NormalizeRuntimeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "Du kan fortfarande använda modulen, men order-, faktura- och andra tenantberoende signaler visas först när Jeeves-kopplingen fungerar igen.";

        return detail.Trim() switch
        {
            "User session is missing." => "Din användarsession saknas. Logga in igen och prova på nytt.",
            "User company is missing." => "Det finns inget aktivt portalbolag kopplat till användaren.",
            "Active connection string mapping is missing." => "Det finns ingen aktiv connection string vald för bolaget.",
            "Company could not be resolved." => "Portalbolaget kunde inte läsas in.",
            "No allowed Jeeves company could be resolved." => "Det finns inget tillåtet Jeeves-bolag kopplat till användaren för det här portalbolaget.",
            "Runtime context resolution failed." => "Det gick inte att bygga tenantkontexten för valt bolag.",
            _ => detail
        };
    }
}
