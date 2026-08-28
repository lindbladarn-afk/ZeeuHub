using Microsoft.AspNetCore.Routing;
using WebApp.Controllers;
using WebApp.Models.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public static class FlowEngineRuntimeEventFactory
{
    public static SidebarRuntimeEventRecord CreateQueued(Guid companyId, FlowEngineJobSnapshot job, LinkGenerator linkGenerator)
        => Create(companyId, job, linkGenerator, FlowEngineJobStatus.Queued);

    public static SidebarRuntimeEventRecord CreateRunning(Guid companyId, FlowEngineJobSnapshot job, LinkGenerator linkGenerator)
        => Create(companyId, job, linkGenerator, FlowEngineJobStatus.Running);

    public static SidebarRuntimeEventRecord CreateCompleted(Guid companyId, FlowEngineJobSnapshot job, LinkGenerator linkGenerator)
        => Create(companyId, job, linkGenerator, FlowEngineJobStatus.Succeeded);

    public static SidebarRuntimeEventRecord CreateFailed(Guid companyId, FlowEngineJobSnapshot job, LinkGenerator linkGenerator)
        => Create(companyId, job, linkGenerator, FlowEngineJobStatus.Failed);

    private static SidebarRuntimeEventRecord Create(
        Guid companyId,
        FlowEngineJobSnapshot job,
        LinkGenerator linkGenerator,
        FlowEngineJobStatus state)
    {
        var occurredAtUtc = job.FinishedAtUtc ?? job.StartedAtUtc ?? job.CreatedAtUtc;
        var source = FlowEngineJobPresentation.GetSystemLabel(job);
        var title = string.IsNullOrWhiteSpace(job.UiLabel) ? (job.Name ?? "FlowEngine-körning") : job.UiLabel;
        var aggregateKey = $"flowengine-job:{job.Id:N}";

        var (statusLabel, statusTone, summary) = state switch
        {
            FlowEngineJobStatus.Queued => ("Queued", "info", $"FlowEngine väntar på att starta '{title}'."),
            FlowEngineJobStatus.Running => ("Running", "info", $"FlowEngine kör '{title}' just nu."),
            FlowEngineJobStatus.Succeeded => ("Completed", "success", $"FlowEngine körde klart '{title}'."),
            FlowEngineJobStatus.Failed => ("Failed", "danger", string.IsNullOrWhiteSpace(job.ErrorMessage)
                ? $"FlowEngine misslyckades med '{title}'."
                : job.ErrorMessage!),
            _ => (FlowEngineJobPresentation.GetStatusLabel(state), "muted", $"FlowEngine uppdaterade '{title}'.")
        };

        return new SidebarRuntimeEventRecord
        {
            CompanyId = companyId,
            AggregateKey = aggregateKey,
            OccurredAtUtc = occurredAtUtc,
            Source = source,
            Title = title,
            Summary = summary,
            LinkUrl = BuildLink(job, linkGenerator),
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            IconClass = "fa fa-bolt"
        };
    }

    private static string? BuildLink(FlowEngineJobSnapshot job, LinkGenerator linkGenerator)
    {
        var action = FlowEngineJobPresentation.GetSystemLabel(job) switch
        {
            "Jeeves" => nameof(IntegrationController.FlowEngineJeeves),
            "Centra" => nameof(IntegrationController.FlowEngineCentra),
            "Shopify" => nameof(IntegrationController.FlowEngineShopify),
            "Akeneo" => nameof(IntegrationController.FlowEngineAkeneo),
            _ => nameof(IntegrationController.FlowEngine)
        };

        return linkGenerator.GetPathByAction(
            action,
            "Integration",
            new { selectedJobId = job.Id });
    }
}
