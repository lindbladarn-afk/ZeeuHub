using WebApp.Repositories.PurchasePrice;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.PurchasePrice
{
    // Registers purchase price import, edit-session, staging, and Excel Import adapter services.
    public static class PurchasePriceServiceCollectionExtensions
    {
        public static IServiceCollection AddPurchasePriceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IPurchasePriceStagingRepository, JeevesPurchasePriceStagingRepository>();
            services.AddScoped<IPurchasePriceStagingRowFactory, PurchasePriceStagingRowFactory>();
            services.AddScoped<IPurchasePriceImportService, PurchasePriceImportService>();
            services.AddScoped<IPurchasePriceEditSessionService, PurchasePriceEditSessionService>();
            services.AddScoped<IExcelImportHandler, PurchasePriceImportHandler>();

            return services;
        }
    }
}
