using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Services.DocumentSigning;

// Describes which document signing background job updates should reach the runtime status menu.
public sealed class DocumentSigningBackgroundJobPresentationProvider : IBackgroundJobPresentationProvider
{
    public string JobType => DocumentSigningBackgroundJobConstants.StatusSyncJobType;

    public SidebarRuntimeEventRecord? CreateEvent(
        BackgroundJobSnapshot job,
        BackgroundJobStatus status,
        string? resultJson,
        string? errorMessage)
    {
        if (status == BackgroundJobStatus.Completed)
        {
            var result = DocumentSigningStatusSyncBackgroundJobResult.FromJson(resultJson);
            return result?.StatusChanged == true
                ? DocumentSigningRuntimeEventFactory.CreateStatusChangedEvent(result.ToListItem())
                : null;
        }

        if (status != BackgroundJobStatus.Failed)
            return null;

        var payload = DocumentSigningStatusSyncBackgroundJobPayload.Deserialize(job.PayloadJson);
        return new SidebarRuntimeEventRecord
        {
            CompanyId = job.CompanyId,
            AggregateKey = $"documentsigning-statussync:{payload.SigningId:N}",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Source = "Oneflow",
            Title = "Statussynk misslyckades",
            Summary = string.IsNullOrWhiteSpace(errorMessage)
                ? "Oneflow-status kunde inte uppdateras just nu."
                : errorMessage,
            LinkUrl = $"/Integration/DocumentSigning?selectedSigningId={payload.SigningId}",
            StatusLabel = "Failed",
            StatusTone = "danger",
            IconClass = "fas fa-file-signature"
        };
    }
}
