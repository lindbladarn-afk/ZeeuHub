using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.Invoices;

namespace WebApp.Repositories.Invoices
{
    public interface IInvoiceDataRepository
    {
        Task<IReadOnlyList<InvoiceDto>> GetAllInvoicesAsync(string connectionString, GetInvoicesQuery query);
        Task<PagedInvoicesResultDto> GetInvoicesPageAsync(string connectionString, GetInvoicesQuery query);
        Task<InvoiceDto?> GetInvoiceAsync(string connectionString, GetInvoiceQuery query);
        Task<DateTime?> GetLatestInvoiceDateAsync(string connectionString, int? companyCode);
        Task<InvoiceDashboardSummaryDto> GetDashboardSummaryAsync(string connectionString, int? companyCode);
    }
}
