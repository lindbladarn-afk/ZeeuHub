using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.HubSpot;
using WebApp.Services.Integration.CustomerSync.Mapping;
using WebApp.Services.Integration.CustomerSync.Presentation;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Services.Integration.CustomerSync;

// Registers the isolated CustomerSync module without leaking its internals into other integrations.
public static class CustomerSyncServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerSyncServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CustomerSyncOptions>(configuration.GetSection(CustomerSyncOptions.SectionName));

        services.AddScoped<CustomerSyncPolicy>();
        services.AddScoped<ICustomerSyncNormalizer, CustomerSyncNormalizer>();
        services.AddScoped<ICustomerSyncMapper, CustomerSyncMapper>();

        services.AddScoped<ICustomerSyncCheckpointRepository, CustomerSyncCheckpointRepository>();
        services.AddScoped<ICustomerSyncMappingRepository, CustomerSyncMappingRepository>();
        services.AddScoped<ICustomerSyncRunRepository, CustomerSyncRunRepository>();
        services.AddScoped<ICustomerSyncEventRepository, CustomerSyncEventRepository>();
        services.AddScoped<ICustomerSyncRuntimeConfigurationRepository, CustomerSyncRuntimeConfigurationRepository>();

        services.AddScoped<CustomerSyncFromJeevesHandler>();
        services.AddScoped<CustomerSyncFromHubSpotHandler>();
        services.AddScoped<CustomerSyncJobScheduler>();
        services.AddScoped<IHubSpotCustomerClient, HubSpotCustomerClient>();
        services.AddScoped<ICustomerSyncHubSpotImportService, CustomerSyncHubSpotImportService>();
        services.AddSingleton<CustomerSyncConfigurationPresenter>();
        services.AddScoped<CustomerSyncPagePresenter>();
        services.AddScoped<ICustomerSyncRuntimeConfigurationService, CustomerSyncRuntimeConfigurationService>();
        services.AddScoped<IBackgroundJobHandler, CustomerSyncBackgroundJobHandler>();
        services.AddScoped<IBackgroundJobPresentationProvider, CustomerSyncPresentationProvider>();
        services.AddHostedService<CustomerSyncHourlyWorker>();
        services.AddHttpClient("Integration.HubSpot");

        return services;
    }
}
