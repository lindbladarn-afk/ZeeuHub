using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

// Handles coding rule mutations for the bank reconciliation workflow.
public interface IBankReconciliationCodingRuleCommandService
{
    Task<BankReconciliationCodingRuleCommandResult> SaveAsync(
        UserSession? user,
        BankReconciliationCodingRuleSaveRequest? request,
        CancellationToken cancellationToken);
}
