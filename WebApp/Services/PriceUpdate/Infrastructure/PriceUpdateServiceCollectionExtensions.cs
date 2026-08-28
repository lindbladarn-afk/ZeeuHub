using WebApp.Repositories.PriceUpdate;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PriceUpdate
{
    // Registers price update import, edit-session, staging, and Excel Import adapter services.
    public static class PriceUpdateServiceCollectionExtensions
    {
        public static IServiceCollection AddPriceUpdateServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IPriceUpdateStagingRepository, JeevesPriceUpdateStagingRepository>();
            services.AddScoped<IPriceUpdateStagingRowFactory, PriceUpdateStagingRowFactory>();
            services.AddScoped<IPriceUpdateImportService, PriceUpdateImportService>();
            services.AddScoped<IPriceUpdateEditSessionService, PriceUpdateEditSessionService>();
            services.AddScoped<IExcelImportHandler, PriceUpdateImportHandler>();

            return services;
        }
    }
}
