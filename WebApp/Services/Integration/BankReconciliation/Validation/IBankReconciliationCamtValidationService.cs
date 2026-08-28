namespace WebApp.Services.Integration.BankReconciliation.Validation;

// Validates CAMT document structure and accounting integrity before transaction extraction.
public interface IBankReconciliationCamtValidationService
{
    BankReconciliationCamtValidationResult Validate(string filePath);
}
