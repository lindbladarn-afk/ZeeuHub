using WebApp.Models.NotifyMe;
using WebApp.Repositories.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Registers NotifyMe services, repositories, and its automation worker.
public static class NotifyMeServiceCollectionExtensions
{
    public static IServiceCollection AddNotifyMeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotifyMeAutomationOptions>(configuration.GetSection(NotifyMeAutomationOptions.SectionName));
        services.AddScoped<INotifyMeRepository, PortalNotifyMeRepository>();
        services.AddScoped<INotifyMePageQueryService, NotifyMePageQueryService>();
        services.AddScoped<INotifyMeSourceQueryRunner, NotifyMeSourceQueryRunner>();
        services.AddScoped<INotifyMeMailRenderer, NotifyMeMailRenderer>();
        services.AddSingleton<INotifyMeRetryPolicy, NotifyMeRetryPolicy>();
        services.AddScoped<INotifyMeExecutionService, PortalNotifyMeExecutionService>();
        services.AddScoped<INotifyMeService, NotifyMeService>();
        services.AddSingleton<INotifyMeDemoService, NotifyMeDemoService>();
        services.AddHostedService<NotifyMeAutomationWorker>();

        return services;
    }
}
