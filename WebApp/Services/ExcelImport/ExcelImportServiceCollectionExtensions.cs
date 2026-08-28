using WebApp.Services.Application.BackgroundJobs;
using WebApp.Repositories.PressKogyoPrice;
using WebApp.Repositories.TransAutoPrice;
using WebApp.Services.PressKogyoPrice;
using WebApp.Services.TransAutoPrice;

namespace WebApp.Services.ExcelImport;

// Registers Excel Import services and background job adapters for the portal module.
public static class ExcelImportServiceCollectionExtensions
{
    public static IServiceCollection AddExcelImportServices(this IServiceCollection services)
    {
        services.AddOptions<ExcelImportBackgroundFileStoreOptions>()
            .BindConfiguration(ExcelImportBackgroundFileStoreOptions.SectionName);
        services.AddOptions<ExcelImportSchemaInitializationOptions>()
            .BindConfiguration(ExcelImportSchemaInitializationOptions.SectionName);
        services.AddHttpContextAccessor();
        services.AddSingleton<IExcelImportTransientStatusStore, ExcelImportTransientStatusStore>();
        services.AddScoped<IExcelImportConnectionResolver, ExcelImportConnectionResolver>();
        services.AddScoped<IExcelImportTableInitializationService, ExcelImportTableInitializationService>();
        services.AddScoped<IExcelImportContextService, ExcelImportContextService>();
        services.AddScoped<IExcelImportWorkbookFileParser, OpenXmlExcelImportWorkbookParser>();
        services.AddScoped<IExcelImportWorkbookFileParser, LegacyXlsExcelImportWorkbookParser>();
        services.AddScoped<IExcelImportWorkbookFileParser, CsvExcelImportWorkbookParser>();
        services.AddScoped<IExcelImportWorkbookReader, ExcelImportWorkbookReader>();
        services.AddScoped<IExcelImportResultFactory, ExcelImportResultFactory>();
        services.AddScoped<IExcelImportFixedTemplateEngine, ExcelImportFixedTemplateEngine>();
        services.AddScoped<IExcelImportEditSessionEngine, ExcelImportEditSessionEngine>();
        services.AddScoped<IExcelImportRowResultStore, JeevesExcelImportRowResultStore>();
        services.AddScoped<IExcelImportRuntimeStatusService, ExcelImportRuntimeStatusService>();
        services.AddScoped<IPressKogyoPriceStagingRepository, JeevesPressKogyoPriceStagingRepository>();
        services.AddScoped<IPressKogyoPriceImportService, PressKogyoPriceImportService>();
        services.AddScoped<ITransAutoPriceStagingRepository, JeevesTransAutoPriceStagingRepository>();
        services.AddScoped<ITransAutoPriceImportService, TransAutoPriceImportService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IExcelImportHandler, PressKogyoPriceImportHandler>();
        services.AddScoped<IExcelImportHandler, TransAutoPriceImportHandler>();
        services.AddScoped<ExcelImportEditSessionAdapterResolver>();
        services.AddScoped<IExcelImportEditSessionAdapter, PriceUpdateEditSessionAdapter>();
        services.AddScoped<IExcelImportEditSessionAdapter, VoucherEditSessionAdapter>();
        services.AddScoped<IExcelImportEditSessionAdapter, PurchasePriceEditSessionAdapter>();
        services.AddScoped<IExcelImportEditSessionAdapter, BudgetEditSessionAdapter>();
        services.AddScoped<IExcelImportEditSessionAdapter, TransAutoPriceEditSessionAdapter>();
        services.AddScoped<IExcelImportEditSessionAdapter, PressKogyoPriceEditSessionAdapter>();
        services.AddScoped<IExcelImportBackgroundFileStore, LocalExcelImportBackgroundFileStore>();
        services.AddScoped<IExcelImportBackgroundJobScheduler, ExcelImportBackgroundJobScheduler>();
        services.AddScoped<IBackgroundJobHandler, ExcelImportBackgroundJobHandler>();
        services.AddScoped<IBackgroundJobPresentationProvider, ExcelImportBackgroundJobPresentationProvider>();

        return services;
    }
}
