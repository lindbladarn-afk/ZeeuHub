using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Bundles;

// Coordinates payment bundle discovery and atomic confirmation for the current reconciliation source.
public interface IBankReconciliationPaymentBundleService
{
    Task<BankReconciliationPaymentBundleQueryResult> BuildSuggestionsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        CancellationToken cancellationToken);

    Task<BankReconciliationPaymentBundleCommandResult> ConfirmAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationConfirmPaymentBundleRequest request,
        CancellationToken cancellationToken);

    Task<BankReconciliationPaymentBundleCommandResult> ConfirmManualAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationConfirmManualPaymentBundleRequest request,
        CancellationToken cancellationToken);
}
