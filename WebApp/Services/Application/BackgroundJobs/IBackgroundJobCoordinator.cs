namespace WebApp.Services.Application.BackgroundJobs;

public interface IBackgroundJobCoordinator
{
    Task RunAsync(CancellationToken stoppingToken);
}
