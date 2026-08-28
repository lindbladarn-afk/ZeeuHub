using WebApp.Models.Invoices;
using WebApp.Repositories.Invoices;

namespace WebApp.Services.Invoices
{
    // Registers invoice repositories, source selection, and application services.
    public static class InvoicesServiceCollectionExtensions
    {
        public static IServiceCollection AddInvoiceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InvoicesFeatureOptions>(configuration.GetSection("Features:Invoices"));
            services.Configure<InvoicesJeevesOptions>(configuration.GetSection("Features:Invoices:Jeeves"));

            services.AddScoped<ILegacyInvoicesRepository, LegacyInvoicesRepository>();
            services.AddScoped<IBiInvoicesRepository, BiInvoicesRepository>();
            services.AddScoped<IInvoiceSourceSelector, InvoiceSourceSelector>();
            services.AddScoped<IInvoicesService, JeevesInvoicesService>();

            return services;
        }
    }
}
