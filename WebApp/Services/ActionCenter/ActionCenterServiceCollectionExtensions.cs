using WebApp.Services.Operations;

namespace WebApp.Services.ActionCenter
{
    // Registers Action Center aggregation, state, seen tracking, and insight providers.
    public static class ActionCenterServiceCollectionExtensions
    {
        public static IServiceCollection AddActionCenterServices(this IServiceCollection services)
        {
            services.AddScoped<IInsightAggregationService, InsightAggregationService>();
            services.AddScoped<IActionCenterService, ActionCenterService>();
            services.AddScoped<IZeeuOperationsService, ZeeuOperationsService>();
            services.AddScoped<IActionCenterSeenStore, IdentityActionCenterSeenStore>();
            services.AddScoped<IActionCenterStateStore, ActionCenterStateStore>();
            services.AddScoped<IInsightProvider, InvoiceInsightProvider>();
            services.AddScoped<IInsightProvider, OrderDeliveryInsightProvider>();
            services.AddScoped<IInsightProvider, FlowEngineInsightProvider>();
            services.AddScoped<IInsightProvider, NotifyMeInsightProvider>();
            services.AddScoped<IInsightProvider, PurchaseApprovalInsightProvider>();
            services.AddScoped<IInsightProvider, MockInsightProvider>();
            services.AddScoped<IInsightProvider, AiQuotaInternalInsightProvider>();
            services.AddScoped<IInsightProvider, PlatformHealthInternalInsightProvider>();
            services.AddScoped<IInsightProvider, AiQueryFailuresInternalInsightProvider>();
            services.AddScoped<IInsightProvider, ExcelImportInternalInsightProvider>();

            return services;
        }
    }
}
