using Entities.Application;
using WebApp.Models.Integration;
using WebApp.ViewModels.Integration.BankReconciliation;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds the invoice detail page for bank reconciliation.
public interface IBankReconciliationInvoiceDetailPageQueryService
{
    Task<BankReconciliationInvoicePageViewModel> BuildPageAsync(
        UserSession? user,
        BankReconciliationSourceContext source,
        CancellationToken cancellationToken);
}
