using WebApp.Models.DocumentSigning;
using WebApp.Repositories.DocumentSigning;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Services;

// Registers Document Signing services, repository adapters, and background job presentation.
public static class DocumentSigningServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentSigningServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DocumentSigningOptions>(configuration.GetSection(DocumentSigningOptions.SectionName));
        services.Configure<BankReconciliationMatchingOptions>(configuration.GetSection(BankReconciliationMatchingOptions.SectionName));
        services.AddHttpClient("Integration.DocumentSigning");
        services.AddScoped<DocumentSigning.OneflowDocumentSigningClient>();
        services.AddScoped<IDocumentSigningRepository, DocumentSigningRepository>();
        services.AddScoped<DocumentSigning.DocumentSigningStatusSyncJobScheduler>();
        services.AddScoped<IBackgroundJobHandler, DocumentSigning.DocumentSigningStatusSyncBackgroundJobHandler>();
        services.AddScoped<IBackgroundJobPresentationProvider, DocumentSigning.DocumentSigningBackgroundJobPresentationProvider>();
        services.AddScoped<DocumentSigning.IDocumentSigningService, DocumentSigning.DocumentSigningService>();
        services.AddScoped<IBankReconciliationMatchingService, BankReconciliationMatchingService>();

        return services;
    }
}
