using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

public interface IBackgroundJobRuntimeEventStore
{
    void Record(BackgroundJobRuntimeEventRecord record);
    IReadOnlyList<BackgroundJobRuntimeEventRecord> ListRecent(Guid companyId, int take);
}
