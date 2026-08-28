namespace WebApp.Services.Integration.BankReconciliation.Imports;

// Registers validated CAMT imports atomically per company and bank account.
public interface IBankReconciliationImportRegistry
{
    Task<BankReconciliationImportRegistrationResult> RegisterAsync(
        BankReconciliationImportRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
