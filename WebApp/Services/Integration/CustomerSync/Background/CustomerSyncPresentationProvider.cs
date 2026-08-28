using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;

namespace WebApp.Services.Integration.CustomerSync.Background;

// Converts CustomerSync background job state into the portal runtime status format.
public sealed class CustomerSyncPresentationProvider : IBackgroundJobPresentationProvider
{
    public string JobType => CustomerSyncBackgroundJobConstants.ExecuteJobType;

    public SidebarRuntimeEventRecord? CreateEvent(
        BackgroundJobSnapshot job,
        BackgroundJobStatus status,
        string? resultJson,
        string? errorMessage)
    {
        var result = CustomerSyncResult.FromJson(resultJson);
        var (label, tone, title) = status switch
        {
            BackgroundJobStatus.Completed => ("Slutförd", "success", "CustomerSync är klar"),
            BackgroundJobStatus.Failed => ("Misslyckad", "danger", "CustomerSync misslyckades"),
            BackgroundJobStatus.Running => ("Pågår", "info", "CustomerSync körs"),
            _ => ("Köad", "info", "CustomerSync är köad")
        };

        return new SidebarRuntimeEventRecord
        {
            CompanyId = job.CompanyId,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            AggregateKey = job.CorrelationKey,
            Source = "CustomerSync",
            Title = title,
            Summary = status == BackgroundJobStatus.Failed
                ? errorMessage ?? "CustomerSync misslyckades."
                : string.IsNullOrWhiteSpace(result.Summary)
                    ? "CustomerSync har uppdaterats."
                    : result.Summary,
            StatusLabel = label,
            StatusTone = tone,
            IconClass = "fa fa-refresh"
        };
    }
}
