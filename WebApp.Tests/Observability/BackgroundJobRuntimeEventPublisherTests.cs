using Entities.Application;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies Excel Import runtime events are routed to the transient cache instead of the sidebar DB store.
public sealed class BackgroundJobRuntimeEventPublisherTests
{
    [Fact]
    public void Publish_For_ExcelImport_Uses_Transient_Store()
    {
        var transientStore = new FakeTransientStatusStore();
        var sidebarStore = new FakeSidebarRuntimeStatusService();
        var publisher = new BackgroundJobRuntimeEventPublisher(
            [new FakeProvider()],
            transientStore,
            sidebarStore,
            NullLogger<BackgroundJobRuntimeEventPublisher>.Instance);

        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType
        };

        publisher.Publish(job, BackgroundJobStatus.Running);

        Assert.Equal(1, transientStore.Records.Count);
        Assert.Empty(sidebarStore.Records);
    }

    private sealed class FakeProvider : IBackgroundJobPresentationProvider
    {
        public string JobType => ExcelImportBackgroundJobConstants.ExecuteJobType;

        public SidebarRuntimeEventRecord? CreateEvent(BackgroundJobSnapshot job, BackgroundJobStatus status, string? resultJson, string? errorMessage)
            => new()
            {
                CompanyId = job.CompanyId,
                OccurredAtUtc = DateTime.UtcNow,
                AggregateKey = $"excel-import:{job.Id:N}",
                Source = "ExcelImport",
                Title = "Excelimport",
                Summary = status.ToString(),
                StatusLabel = status.ToString(),
                StatusTone = "info",
                IconClass = "fa fa-file-excel",
                LinkUrl = "/ExcelImport"
            };
    }

    private sealed class FakeTransientStatusStore : IExcelImportTransientStatusStore
    {
        public List<SidebarRuntimeEventRecord> Records { get; } = new();

        public void ClearCompany(Guid companyId)
        {
        }

        public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecent(Guid companyId, int take = 5)
            => Array.Empty<SidebarRuntimeStatusItemViewModel>();

        public IReadOnlyList<SidebarRuntimeStatusItemViewModel> ListRecentSummaries(Guid companyId, int take = 5)
            => Array.Empty<SidebarRuntimeStatusItemViewModel>();

        public void Record(SidebarRuntimeEventRecord record)
        {
            Records.Add(record);
        }
    }

    private sealed class FakeSidebarRuntimeStatusService : ISidebarRuntimeStatusService
    {
        public List<SidebarRuntimeEventRecord> Records { get; } = new();

        public SidebarRuntimeStatusViewModel GetStatus(UserSession? sessionUser) => new();

        public Task<SidebarRuntimeStatusViewModel> GetStatusAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(new SidebarRuntimeStatusViewModel());

        public void MarkAllRead(UserSession sessionUser)
        {
        }

        public void RecordEvent(UserSession sessionUser, SidebarRuntimeEventRecord record)
        {
            Records.Add(record);
        }

        public void RecordEvent(Guid companyId, SidebarRuntimeEventRecord record)
        {
            Records.Add(record);
        }
    }
}
