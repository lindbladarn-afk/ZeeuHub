namespace WebApp.Services.Application.BackgroundJobs;

// Configures how the shared background job worker partitions and polls company lanes.
public sealed class BackgroundJobWorkerOptions
{
    public int MaxConcurrentCompanyLanes { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    public int MaxDiscoveredCompaniesPerTick { get; set; } = 25;
    public int CompanyLaneIdleAttempts { get; set; } = 2;
    public TimeSpan DispatcherIdleDelay { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan DispatcherBusyDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan CompanyLaneIdleDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan ExpiredLeaseRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}
