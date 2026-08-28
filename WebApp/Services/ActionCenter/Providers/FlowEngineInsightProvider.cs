using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Surfaces failed FlowEngine jobs in Action Center so users can jump straight to the affected section.
/// </summary>
public sealed class FlowEngineInsightProvider : IInsightProvider
{
    private readonly IFlowEngineJobStore _jobStore;

    public string ProviderKey => "customer-flowengine";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public FlowEngineInsightProvider(IFlowEngineJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(
        UserSession user,
        JeevesRuntimeContext? runtimeContext,
        CancellationToken cancellationToken)
    {
        if (user.CompanyId is not Guid companyId || companyId == Guid.Empty)
            return Task.FromResult<IEnumerable<ActionCenterInsight>>(Array.Empty<ActionCenterInsight>());

        var jobs = _jobStore.ListRecent(companyId, 100);
        if (jobs.Count == 0)
            return Task.FromResult<IEnumerable<ActionCenterInsight>>(Array.Empty<ActionCenterInsight>());

        var failedBySystem = jobs
            .Where(x => x.Status == FlowEngineJobStatus.Failed)
            .Select(job => new
            {
                Job = job,
                System = ResolveSystem(job),
                Section = ResolveSection(job)
            })
            .GroupBy(x => x.System, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.Job.FinishedAtUtc ?? x.Job.CreatedAtUtc)
                .First())
            .OrderByDescending(x => x.Job.FinishedAtUtc ?? x.Job.CreatedAtUtc)
            .ToList();

        if (failedBySystem.Count == 0)
            return Task.FromResult<IEnumerable<ActionCenterInsight>>(Array.Empty<ActionCenterInsight>());

        var insights = failedBySystem.Select(x =>
        {
            var occurredAt = (x.Job.FinishedAtUtc ?? x.Job.CreatedAtUtc).UtcDateTime;
            var error = x.Job.ErrorMessage;
            if (string.IsNullOrWhiteSpace(error))
                error = x.Job.Result?.StandardError;
            if (string.IsNullOrWhiteSpace(error))
                error = "FlowEngine-körningen misslyckades. Öppna sektionen för detaljer och ny körning.";

            return new ActionCenterInsight
            {
                Key = $"flowengine-failed-{x.System.ToLowerInvariant()}",
                Audience = ActionCenterAudience.Customer,
                Category = "FlowEngine",
                Status = ActionCenterStatus.Open,
                Title = $"{x.System} kräver åtgärd",
                Description = Trim(error, 220),
                Priority = ActionCenterPriority.Medium,
                DetectedAt = occurredAt,
                DueAt = occurredAt,
                LinkText = "Öppna FlowEngine",
                LinkUrl = ResolveSectionUrl(x.Section)
            };
        }).ToList();

        return Task.FromResult<IEnumerable<ActionCenterInsight>>(insights);
    }

    private static string ResolveSystem(FlowEngineJobSnapshot job)
    {
        var label = $"{job.UiLabel} {job.Name}".Trim();
        if (label.Contains("shopify", StringComparison.OrdinalIgnoreCase))
            return "Shopify";
        if (label.Contains("centra", StringComparison.OrdinalIgnoreCase))
            return "Centra";
        if (label.Contains("akeneo", StringComparison.OrdinalIgnoreCase))
            return "Akeneo";
        if (label.Contains("jeeves", StringComparison.OrdinalIgnoreCase))
            return "Jeeves";
        return "FlowEngine";
    }

    private static string ResolveSection(FlowEngineJobSnapshot job)
    {
        var system = ResolveSystem(job);
        return system switch
        {
            "Shopify" => FlowEngineSectionKeys.Shopify,
            "Centra" => FlowEngineSectionKeys.Centra,
            "Akeneo" => FlowEngineSectionKeys.Akeneo,
            "Jeeves" => FlowEngineSectionKeys.Jeeves,
            _ => FlowEngineSectionKeys.Dashboard
        };
    }

    private static string ResolveSectionUrl(string section)
    {
        return section switch
        {
            FlowEngineSectionKeys.Shopify => "/Integration/FlowEngineShopify",
            FlowEngineSectionKeys.Centra => "/Integration/FlowEngineCentra",
            FlowEngineSectionKeys.Akeneo => "/Integration/FlowEngineAkeneo",
            FlowEngineSectionKeys.Jeeves => "/Integration/FlowEngineJeeves",
            _ => "/Integration/FlowEngine"
        };
    }

    private static string Trim(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";
}
