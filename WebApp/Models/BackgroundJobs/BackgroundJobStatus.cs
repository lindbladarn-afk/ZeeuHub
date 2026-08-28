namespace WebApp.Models.BackgroundJobs;

public enum BackgroundJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4
}
