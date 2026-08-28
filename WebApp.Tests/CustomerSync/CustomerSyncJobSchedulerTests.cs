using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncJobSchedulerTests
{
    [Fact]
    public void EnqueueJeevesToHubSpotIfMissing_Reuses_Active_Job()
    {
        var store = new FakeBackgroundJobStore();
        var scheduler = new CustomerSyncJobScheduler(store);
        var companyId = Guid.NewGuid();

        var first = scheduler.EnqueueJeevesToHubSpotIfMissing(companyId, 1, CustomerSyncTrigger.Scheduled, DateTime.UtcNow);
        var second = scheduler.EnqueueJeevesToHubSpotIfMissing(companyId, 1, CustomerSyncTrigger.Scheduled, DateTime.UtcNow);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(store.Enqueued);
        Assert.Equal(CustomerSyncBackgroundJobConstants.ExecuteJobType, first.JobType);
    }

    [Fact]
    public void BuildBatchCorrelationKey_Includes_Direction_Company_And_JeevesCompany()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var key = CustomerSyncJobScheduler.BuildBatchCorrelationKey(
            companyId,
            4,
            CustomerSyncDirection.JeevesToHubSpot);

        Assert.Equal("customersync:jeevestohubspot:aaaaaaaabbbbccccddddeeeeeeeeeeee:4", key);
    }

    private sealed class FakeBackgroundJobStore : IBackgroundJobStore
    {
        public List<BackgroundJobSnapshot> Enqueued { get; } = new();

        public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow)
        {
            var snapshot = new BackgroundJobSnapshot
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                CreatedByUserId = request.CreatedByUserId,
                CreatedByEmail = request.CreatedByEmail,
                JobType = request.JobType,
                CorrelationKey = request.CorrelationKey,
                PayloadJson = request.PayloadJson,
                MaxAttempts = request.MaxAttempts,
                Status = BackgroundJobStatus.Queued,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
                QueuedAtUtc = utcNow,
                AvailableAtUtc = request.AvailableAtUtc ?? utcNow
            };

            Enqueued.Add(snapshot);
            return snapshot;
        }

        public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null)
            => Enqueued.FirstOrDefault(item =>
                item.CompanyId == companyId
                && item.JobType == jobType
                && item.CorrelationKey == correlationKey
                && item.Status is BackgroundJobStatus.Queued or BackgroundJobStatus.Running
                && item.Id != excludeJobId.GetValueOrDefault());

        public BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null)
            => throw new NotSupportedException();

        public IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null)
            => Array.Empty<Guid>();

        public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId)
            => throw new NotSupportedException();

        public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take)
            => throw new NotSupportedException();

        public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null)
            => throw new NotSupportedException();

        public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay)
            => throw new NotSupportedException();
    }
}
