using System;
using System.Threading.Tasks;
using WebApp.Models.Invoices;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.Invoices
{
    public interface IInvoicesService
    {
        Task<InvoiceListViewModel> GetInvoiceListAsync(string connectionString, GetInvoicesQuery query);
        Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo);
        Task<InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode);
    }
}
