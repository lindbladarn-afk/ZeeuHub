using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

// Publishes background job lifecycle updates to the shared runtime status surface.
public interface IBackgroundJobRuntimeEventPublisher
{
    void Publish(BackgroundJobSnapshot job, BackgroundJobStatus status, string? resultJson = null, string? errorMessage = null);
}
