using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds paged invoice results for bank reconciliation invoice browsing.
public interface IBankReconciliationInvoicePageQueryService
{
    Task<BankReconciliationInvoicePageQueryResult> BuildPageAsync(
        UserSession? user,
        int page,
        int pageSize,
        string? classificationFilter,
        string? groupFilter,
        CancellationToken cancellationToken);
}
