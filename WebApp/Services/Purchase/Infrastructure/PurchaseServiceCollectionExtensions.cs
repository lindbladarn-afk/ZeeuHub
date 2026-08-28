using WebApp.Services.Purchase.Demo;

using WebApp.Services.Purchase.Context;
using WebApp.Services.Purchase.Lookup;
using WebApp.Services.Purchase.Orders;

namespace WebApp.Services.Purchase.Infrastructure;

// Keeps Purchase service registrations owned by the Purchase module.
public static class PurchaseServiceCollectionExtensions
{
    public static IServiceCollection AddPurchaseServices(this IServiceCollection services)
    {
        services.AddScoped<IPurchaseContextService, PurchaseContextService>();
        services.AddScoped<IPurchaseDemoDataService, PurchaseDemoDataService>();
        services.AddScoped<IPurchaseDemoModeService, PurchaseDemoModeService>();
        services.AddScoped<IPurchaseLookupService, PurchaseLookupService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

        return services;
    }
}
