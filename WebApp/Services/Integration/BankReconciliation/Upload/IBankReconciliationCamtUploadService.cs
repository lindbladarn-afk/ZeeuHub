using Microsoft.AspNetCore.Http;
using WebApp.Services.Integration.BankReconciliation.Imports;

namespace WebApp.Services.Integration.BankReconciliation.Upload;

// Validates and stages uploaded camt.053 files before they become active source data.
public interface IBankReconciliationCamtUploadService
{
    Task<BankReconciliationCamtUploadResult> PrepareUploadAsync(
        IFormFile file,
        Guid companyId,
        string sessionId,
        string? previousFilePath,
        CancellationToken cancellationToken = default);
}

public sealed class BankReconciliationCamtUploadResult
{
    public bool Success { get; init; }
    public string? StoredFilePath { get; init; }
    public int TransactionCount { get; init; }
    public BankReconciliationCamtUploadFailureReason FailureReason { get; init; }
    public string? FailureDetails { get; init; }
    public BankReconciliationImportStatus? ImportStatus { get; init; }
    public int OverlappingTransactionCount { get; init; }
}

public enum BankReconciliationCamtUploadFailureReason
{
    None = 0,
    SaveError = 1,
    ParseError = 2,
    NoTransactionsFound = 3,
    ValidationError = 4,
    FileTooLarge = 5,
    DuplicateImport = 6,
    OverlappingImport = 7,
    MissingCompany = 8
}
