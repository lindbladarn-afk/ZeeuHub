namespace WebApp.Services.Admin
{
    // Registers admin application services for overview, health, users, companies, and event logs.
    public static class AdminServiceCollectionExtensions
    {
        public static IServiceCollection AddAdminServices(this IServiceCollection services)
        {
            services.AddScoped<ApprovalChains.IApprovalChainDesignerService, ApprovalChains.ApprovalChainDesignerService>();
            services.AddScoped<ApprovalChains.IApprovalChainPurchaseDecisionService, ApprovalChains.ApprovalChainPurchaseDecisionService>();
            services.AddScoped<ApprovalChains.IApprovalChainPurchaseJeevesReader, ApprovalChains.ApprovalChainPurchaseJeevesReader>();
            services.AddScoped<ApprovalChains.IApprovalChainPurchaseParityService, ApprovalChains.ApprovalChainPurchaseParityService>();
            services.AddScoped<IAdminOverviewMetricsService, AdminOverviewMetricsService>();
            services.AddScoped<IAdminHealthService, AdminHealthService>();
            services.AddScoped<IAdminCompanyConnectionHealthService, AdminCompanyConnectionHealthService>();
            services.AddScoped<IAdminOverviewService, AdminOverviewService>();
            services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();
            services.AddScoped<IAdminCompanyManagementService, AdminCompanyManagementService>();
            services.AddScoped<IAdminEventLogService, AdminEventLogService>();

            return services;
        }
    }
}
