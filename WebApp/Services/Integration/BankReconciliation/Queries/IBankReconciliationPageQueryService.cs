using Entities.Application;
using WebApp.ViewModels.Integration.BankReconciliation;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds the bank reconciliation start page view model from the current session state.
public interface IBankReconciliationPageQueryService
{
    Task<BankReconciliationPageViewModel> BuildPageAsync(
        UserSession? user,
        string? uploadError,
        string? uploadInfo,
        string? statusMessage,
        string? statusTone,
        CancellationToken cancellationToken);
}
