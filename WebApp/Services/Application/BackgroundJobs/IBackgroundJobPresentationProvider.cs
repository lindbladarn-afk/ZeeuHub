using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

// Converts generic background job state into module-specific sidebar status content.
public interface IBackgroundJobPresentationProvider
{
    string JobType { get; }

    SidebarRuntimeEventRecord? CreateEvent(
        BackgroundJobSnapshot job,
        BackgroundJobStatus status,
        string? resultJson,
        string? errorMessage);
}
