using Microsoft.AspNetCore.Authorization;
using WebApp.Models.ControlPanel;

namespace WebApp.Services.ControlPanel;

// Registers Control Panel access and orchestration services.
public static class ControlPanelServiceCollectionExtensions
{
    public static IServiceCollection AddControlPanelServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ControlPanelOptions>(configuration.GetSection(ControlPanelOptions.SectionName));
        services.AddScoped<IControlPanelAccessService, ControlPanelAccessService>();
        services.AddScoped<IAuthorizationHandler, ControlPanelAccessHandler>();

        return services;
    }
}
