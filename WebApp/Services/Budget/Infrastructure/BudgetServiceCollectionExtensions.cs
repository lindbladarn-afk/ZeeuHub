using WebApp.Repositories.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Budget
{
    // Registers budget import, edit-session, staging, and Excel Import adapter services.
    public static class BudgetServiceCollectionExtensions
    {
        public static IServiceCollection AddBudgetServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IBudgetStagingRepository, JeevesBudgetStagingRepository>();
            services.AddScoped<IBudgetStagingRowFactory, BudgetStagingRowFactory>();
            services.AddScoped<IBudgetImportService, BudgetImportService>();
            services.AddScoped<IBudgetEditSessionService, BudgetEditSessionService>();
            services.AddScoped<IExcelImportHandler, BudgetImportHandler>();

            return services;
        }
    }
}
