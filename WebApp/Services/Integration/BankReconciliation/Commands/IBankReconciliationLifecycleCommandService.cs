// Exposes the controlled close and reopen lifecycle for bank reconciliation.
using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

public interface IBankReconciliationLifecycleCommandService
{
    Task<BankReconciliationLifecycleCommandResult> CloseAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken);

    Task<BankReconciliationLifecycleCommandResult> ReopenAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        string reason,
        CancellationToken cancellationToken);
}
