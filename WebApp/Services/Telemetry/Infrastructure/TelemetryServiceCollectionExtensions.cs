namespace WebApp.Services.Telemetry
{
    // Registers telemetry writers, readers, and the compatibility facade used by portal modules.
    public static class TelemetryServiceCollectionExtensions
    {
        public static IServiceCollection AddTelemetryServices(this IServiceCollection services)
        {
            services.AddScoped<ITelemetryUsageService, TelemetryUsageService>();
            services.AddScoped<ITelemetryExcelImportService, TelemetryExcelImportService>();
            services.AddScoped<ITelemetryAiQueryService, TelemetryAiQueryService>();
            services.AddScoped<ITelemetrySummaryService, TelemetrySummaryService>();
            services.AddScoped<ITelemetryService, TelemetryService>();

            return services;
        }
    }
}
