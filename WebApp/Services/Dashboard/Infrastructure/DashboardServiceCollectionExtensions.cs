// Registers dashboard catalog, layout, runtime, and isolated card provider services.
using WebApp.Models.Dashboard;
using WebApp.Services.Dashboard.Demo;

namespace WebApp.Services.Dashboard;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DashboardDemoOptions>(configuration.GetSection(DashboardDemoOptions.SectionName));
        services.AddScoped<IDashboardConfigurationService, DefaultDashboardConfigurationService>();
        services.AddScoped<IDashboardWidgetLayoutService, DashboardWidgetLayoutService>();
        services.AddScoped<IDashboardDemoDataService, DashboardDemoDataService>();
        services.AddScoped<IMemberDashboardService, MemberDashboardService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DashboardCardViewModelFactory>();
        services.AddScoped<DashboardCardDataContextFactory>();
        services.AddScoped<DashboardCardProviderRegistry>();
        services.AddScoped<IDashboardCardProvider, RevenueDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, ActionCenterDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, CustomerActivityDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, PurchaseApprovalDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, NotifyMeDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, DeliveryStatusDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, InventoryStatusDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, PurchaseAcknowledgementDashboardCardProvider>();
        services.AddScoped<IDashboardCardProvider, DocumentSigningDashboardCardProvider>();

        return services;
    }
}
