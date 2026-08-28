using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Imports;

// Import registry models describe idempotency outcomes without retaining raw bank data.
public sealed class BankReconciliationImportRegistrationRequest
{
    public Guid CompanyId { get; init; }
    public required BankReconciliationParsedDocument Document { get; init; }
}

public sealed class BankReconciliationImportRegistrationResult
{
    public BankReconciliationImportStatus Status { get; init; }
    public int TransactionCount { get; init; }
    public int OverlappingTransactionCount { get; init; }
    public bool Accepted => Status is BankReconciliationImportStatus.New or BankReconciliationImportStatus.Corrected;
}

public enum BankReconciliationImportStatus
{
    New = 0,
    ExactDuplicate = 1,
    Overlapping = 2,
    Corrected = 3
}

internal sealed class BankReconciliationImportRegistryState
{
    public int Version { get; set; }
    public List<BankReconciliationImportRecord> Imports { get; set; } = new();
}

internal sealed class BankReconciliationImportRecord
{
    public string ImportId { get; set; } = Guid.NewGuid().ToString("N");
    public string StatementFingerprint { get; set; } = string.Empty;
    public string DocumentFingerprint { get; set; } = string.Empty;
    public List<string> TransactionFingerprints { get; set; } = new();
    public DateTime ImportedAtUtc { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public string? SupersededByImportId { get; set; }
}
