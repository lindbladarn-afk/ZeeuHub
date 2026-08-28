using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds read-only persisted state responses for bank reconciliation.
public interface IBankReconciliationStateQueryService
{
    Task<BankReconciliationStateQueryResult> BuildStateAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        CancellationToken cancellationToken);
}
