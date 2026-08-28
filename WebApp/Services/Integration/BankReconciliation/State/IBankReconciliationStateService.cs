using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

public interface IBankReconciliationStateService
{
    Task<BankReconciliationPersistedState> LoadAsync(Guid companyId, string stateKey, CancellationToken cancellationToken = default);
    Task<BankReconciliationPersistedState> ReplaceMatchesAsync(Guid companyId, string stateKey, UserSession? user, IReadOnlyList<BankReconciliationSavedMatch> matches, string auditActionType, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default);
    Task<BankReconciliationPersistedState> UpsertMatchAsync(Guid companyId, string stateKey, UserSession? user, BankReconciliationSavedMatch match, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default);
    Task<BankReconciliationPersistedState> ReverseMatchAsync(Guid companyId, string stateKey, UserSession? user, string transactionId, string? allocationId = null, string? invoiceId = null, int? expectedVersion = null, string? reason = null, CancellationToken cancellationToken = default);
    Task<BankReconciliationPersistedState> CloseAsync(Guid companyId, string stateKey, UserSession? user, int? expectedVersion, string sourceFingerprint, int codingRulesVersion, CancellationToken cancellationToken = default);
    Task<BankReconciliationPersistedState> ReopenAsync(Guid companyId, string stateKey, UserSession? user, int? expectedVersion, string reason, CancellationToken cancellationToken = default);
}
