using Microsoft.Extensions.Configuration;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Services.Application.BackgroundJobs
{
    // Registers the shared background job infrastructure used by feature modules.
    public static class BackgroundJobServiceCollectionExtensions
    {
        public static IServiceCollection AddBackgroundJobServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BackgroundJobWorkerOptions>(configuration.GetSection("BackgroundJobs"));
            services.AddScoped<IBackgroundJobStore, BackgroundJobDbStore>();
            services.AddScoped<IBackgroundJobRuntimeEventStore, BackgroundJobRuntimeEventDbStore>();
            services.AddScoped<IBackgroundJobRuntimeEventPublisher, BackgroundJobRuntimeEventPublisher>();
            services.AddScoped<IBackgroundJobHandler, FlowEngineBackgroundJobHandler>();
            services.AddSingleton<IBackgroundJobCoordinator, BackgroundJobCoordinator>();
            services.AddHostedService<BackgroundJobWorker>();

            return services;
        }
    }
}
