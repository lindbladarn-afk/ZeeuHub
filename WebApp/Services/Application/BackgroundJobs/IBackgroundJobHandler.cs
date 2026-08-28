using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task<BackgroundJobHandlerResult> HandleAsync(BackgroundJobSnapshot job, CancellationToken cancellationToken);
}
