namespace WebApp.Services.Integration.BankReconciliation.Validation;

// CAMT validation models separate blocking document errors from reviewable source warnings.
public sealed class BankReconciliationCamtValidationResult
{
    public string? CamtVersion { get; set; }
    public string? StatementId { get; set; }
    public int StatementCount { get; set; }
    public int EntryCount { get; set; }
    public int TransactionCount { get; set; }
    public int BookedEntryCount { get; set; }
    public int BlockedEntryCount { get; set; }
    public string? Currency { get; set; }
    public string? MaskedAccount { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public List<BankReconciliationCamtValidationIssue> Issues { get; } = new();
    public bool IsValid => Issues.All(issue => issue.Severity != BankReconciliationCamtValidationSeverity.Error);
}

public sealed class BankReconciliationCamtValidationIssue
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public BankReconciliationCamtValidationSeverity Severity { get; init; }
}

public enum BankReconciliationCamtValidationSeverity
{
    Warning = 0,
    Error = 1
}

public sealed class BankReconciliationCamtValidationOptions
{
    public const string SectionName = "BankReconciliation:CamtValidation";

    public long MaximumFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public long MaximumXmlCharacters { get; set; } = 10 * 1024 * 1024;
}
