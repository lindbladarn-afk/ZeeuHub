// Runs the shared background job coordinator inside the hosting pipeline.
using Microsoft.Extensions.Hosting;

namespace WebApp.Services.Application.BackgroundJobs;

public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly IBackgroundJobCoordinator _coordinator;

    public BackgroundJobWorker(IBackgroundJobCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _coordinator.RunAsync(stoppingToken);
}
