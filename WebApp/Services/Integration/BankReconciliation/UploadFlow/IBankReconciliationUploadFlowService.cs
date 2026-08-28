using Microsoft.AspNetCore.Http;

namespace WebApp.Services.Integration.BankReconciliation.UploadFlow;

// Manages the session-bound bank reconciliation upload flow.
public interface IBankReconciliationUploadFlowService
{
    Task<BankReconciliationUploadFlowResult> UploadAsync(
        IFormFile? file,
        CancellationToken cancellationToken);

    BankReconciliationUploadFlowResult ClearUpload();

    string? ResolveLatestCamtFile();

    string? ResolveLatestCamtDisplayName();
}
