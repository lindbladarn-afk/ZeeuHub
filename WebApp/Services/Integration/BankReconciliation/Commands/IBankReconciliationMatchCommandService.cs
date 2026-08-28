using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

// Handles match mutations for the bank reconciliation workflow.
public interface IBankReconciliationMatchCommandService
{
    Task<BankReconciliationMatchCommandResult> SaveManualMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationManualMatchRequest request,
        CancellationToken cancellationToken);

    Task<BankReconciliationMatchCommandResult> SaveMatchesAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationSaveMatchesRequest request,
        CancellationToken cancellationToken);

    Task<BankReconciliationMatchCommandResult> ReverseMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationReverseMatchRequest request,
        CancellationToken cancellationToken);

    Task<BankReconciliationMatchCommandResult> AutoMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken);

    Task<BankReconciliationMatchCommandResult> ResetMatchesAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken);
}
