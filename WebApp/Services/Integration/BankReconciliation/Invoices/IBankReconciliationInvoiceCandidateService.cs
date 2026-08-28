using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Invoices;

public interface IBankReconciliationInvoiceCandidateService
{
    Task<BankReconciliationInvoiceCandidateResult> LoadAsync(
        bool isDemoMode,
        UserSession user,
        CancellationToken cancellationToken,
        BankReconciliationParsedTransaction? transaction = null,
        string? classificationFilter = null,
        string? groupFilter = null,
        int? page = null,
        int? pageSize = null,
        string? demoScenarioKey = null);

    Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<BankReconciliationInvoiceCandidateResult> LoadCombinedPageAsync(
        UserSession user,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => LoadAsync(
            false,
            user,
            cancellationToken,
            page: page,
            pageSize: pageSize);
}
