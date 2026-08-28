using WebApp.Models.BackgroundJobs;
using WebApp.Models.DocumentSigning;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Services.DocumentSigning;

public sealed class DocumentSigningStatusSyncJobScheduler
{
    private readonly IBackgroundJobStore _backgroundJobStore;

    public DocumentSigningStatusSyncJobScheduler(IBackgroundJobStore backgroundJobStore)
    {
        _backgroundJobStore = backgroundJobStore;
    }

    public BackgroundJobSnapshot? EnqueueIfMissing(
        DocumentSigningRecord signing,
        TimeSpan? delay = null,
        Guid? excludeJobId = null)
    {
        ArgumentNullException.ThrowIfNull(signing);

        var correlationKey = BuildCorrelationKey(signing.Id);
        var existing = _backgroundJobStore.FindActive(
            signing.CompanyId,
            DocumentSigningBackgroundJobConstants.StatusSyncJobType,
            correlationKey,
            excludeJobId);

        if (existing != null)
            return existing;

        return _backgroundJobStore.Enqueue(
            new BackgroundJobEnqueueRequest
            {
                CompanyId = signing.CompanyId,
                CreatedByUserId = signing.CreatedByUserId,
                CreatedByEmail = signing.CreatedByEmail,
                JobType = DocumentSigningBackgroundJobConstants.StatusSyncJobType,
                CorrelationKey = correlationKey,
                PayloadJson = DocumentSigningStatusSyncBackgroundJobPayload.Serialize(
                    new DocumentSigningStatusSyncBackgroundJobPayload
                    {
                        SigningId = signing.Id
                    }),
                MaxAttempts = 10,
                AvailableAtUtc = DateTime.UtcNow.Add(delay.GetValueOrDefault())
            },
            DateTime.UtcNow);
    }

    public static string BuildCorrelationKey(Guid signingId)
        => $"documentsigning:statussync:{signingId:N}";
}
