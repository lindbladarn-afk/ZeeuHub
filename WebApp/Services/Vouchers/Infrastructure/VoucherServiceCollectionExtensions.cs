using WebApp.Repositories.Vouchers;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers
{
    // Registers voucher import, edit-session, staging, and Excel Import adapter services.
    public static class VoucherServiceCollectionExtensions
    {
        public static IServiceCollection AddVoucherServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IVoucherStagingRepository, JeevesVoucherStagingRepository>();
            services.AddScoped<IVoucherWorkbookReader, VoucherWorkbookReader>();
            services.AddScoped<IVoucherImportResultFactory, VoucherImportResultFactory>();
            services.AddScoped<IVoucherStagingRowFactory, VoucherStagingRowFactory>();
            services.AddScoped<IVoucherImportService, VoucherImportService>();
            services.AddScoped<IVoucherEditSessionService, VoucherEditSessionService>();
            services.AddScoped<IExcelImportHandler, VoucherImportHandler>();

            return services;
        }
    }
}
