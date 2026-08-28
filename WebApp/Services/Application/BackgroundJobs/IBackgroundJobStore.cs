using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

public interface IBackgroundJobStore
{
    BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow);
    BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null);
    IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null);
    BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null);
    BackgroundJobSnapshot? Get(Guid companyId, Guid jobId);
    IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take);
    IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take);
    BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration);
    BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null);
    BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null);
    BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null);
    int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay);
}
