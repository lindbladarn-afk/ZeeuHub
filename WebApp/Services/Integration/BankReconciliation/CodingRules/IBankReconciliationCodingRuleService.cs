using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.CodingRules;

// Persists the bank reconciliation coding matrix per company and bank account.
public interface IBankReconciliationCodingRuleService
{
    Task<BankReconciliationCodingRuleSet> LoadAsync(
        Guid companyId,
        string bankAccountKey,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationCodingRuleSet> SaveAsync(
        Guid companyId,
        string bankAccountKey,
        UserSession? user,
        IReadOnlyList<BankReconciliationCodingRuleRow> rows,
        string? bankAccountLabel = null,
        int? expectedVersion = null,
        CancellationToken cancellationToken = default);
}
