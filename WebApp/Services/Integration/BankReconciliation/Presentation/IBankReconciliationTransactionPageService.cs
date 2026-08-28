using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation.Presentation;

public interface IBankReconciliationTransactionPageService
{
    BankReconciliationTransactionPageResult BuildPage(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        IReadOnlyList<InvoiceItem> invoices,
        int page,
        int pageSize,
        string? filter,
        string? groupFilter,
        string? classificationFilter);

    BankReconciliationTransactionPageResult BuildEmptyPage(int page, int pageSize, string? errorMessage = null);
}
