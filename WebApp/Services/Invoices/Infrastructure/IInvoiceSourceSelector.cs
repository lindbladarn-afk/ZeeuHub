using System.Threading.Tasks;

namespace WebApp.Services.Invoices
{
    public enum InvoiceDataSource
    {
        Legacy = 0,
        Bi = 1
    }

    public interface IInvoiceSourceSelector
    {
        Task<InvoiceDataSource> SelectAsync(string connectionString);
    }
}
