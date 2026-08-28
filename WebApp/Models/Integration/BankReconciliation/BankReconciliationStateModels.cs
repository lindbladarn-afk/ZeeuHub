using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Integration;

// Bank reconciliation state models describe persisted manual matches and deterministic matching inputs.
public sealed class BankReconciliationPersistedState
{
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByUserId { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosedSourceFingerprint { get; set; }
    public int? ClosedCodingRulesVersion { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public string? ReopenedByUserId { get; set; }
    public string? ReopenedByName { get; set; }
    public List<BankReconciliationSavedMatch> Matches { get; set; } = new();
    public List<BankReconciliationAuditEntry> AuditTrail { get; set; } = new();
}

public sealed class BankReconciliationSavedMatch
{
    public string AllocationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TransactionId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string MatchType { get; set; } = "manual";
    public string MatchRule { get; set; } = "manual";
    public decimal MatchedAmount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BankReconciliationAuditEntry
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ActionType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? TransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? MatchType { get; set; }
    public string? MatchRule { get; set; }
    public decimal? MatchedAmount { get; set; }
    public string? Note { get; set; }
}

public sealed class BankReconciliationManualMatchRequest
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;

    [Required]
    public string InvoiceId { get; set; } = string.Empty;

    public decimal? MatchedAmount { get; set; }
    public int? ExpectedVersion { get; set; }
}

public sealed class BankReconciliationSaveMatchesRequest
{
    public int? ExpectedVersion { get; set; }
    public List<BankReconciliationSavedMatchInput> Matches { get; set; } = new();
}

public sealed class BankReconciliationSavedMatchInput
{
    public string TransactionId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string MatchType { get; set; } = "auto";
    public string MatchRule { get; set; } = "auto";
    public decimal? MatchedAmount { get; set; }
}

public sealed class BankReconciliationReverseMatchRequest
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;

    public string? AllocationId { get; set; }
    public string? InvoiceId { get; set; }
    public string? Reason { get; set; }
    public int? ExpectedVersion { get; set; }
}

public sealed class BankReconciliationCloseRequest
{
    public int? ExpectedVersion { get; set; }
}

public sealed class BankReconciliationReopenRequest
{
    public int? ExpectedVersion { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class BankReconciliationTransactionCandidate
{
    public string TransactionId { get; set; } = string.Empty;
    public string? StatementId { get; set; }
    public string? StatementAccountIban { get; set; }
    public string? StatementAccountNumber { get; set; }
    public string? StatementAccountOwner { get; set; }
    public string? StatementBankBic { get; set; }
    public string? Date { get; set; }
    public string? ValueDate { get; set; }
    public string? EntryStatus { get; set; }
    public string? Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }
    public string? EndToEndId { get; set; }
    public string? TransactionIdSource { get; set; }
    public string? AccountServiceReference { get; set; }
    public string? DebtorName { get; set; }
    public string? Remittance { get; set; }
    public List<string> ReferenceCandidates { get; set; } = new();
    public string? ResolvedCodingTypeKey { get; set; }
    public string? ResolvedCodingTypeLabel { get; set; }
    public string? ResolvedCodingAccount { get; set; }
    public string? ResolvedCodingCostCenter { get; set; }
    public bool ResolvedCodingIsDefault { get; set; }
}

public sealed class BankReconciliationMatchSignals
{
    public bool RefExact { get; set; }
    public bool RefPartial { get; set; }
    public bool AmountExact { get; set; }
    public bool AmountTolerance { get; set; }
    public bool CurrencyMatch { get; set; }
    public bool NameMatch { get; set; }
    public bool DateMatch { get; set; }
}

public sealed class BankReconciliationReferenceEvidence
{
    public string TransactionSource { get; set; } = string.Empty;
    public string TransactionValue { get; set; } = string.Empty;
    public string InvoiceSource { get; set; } = string.Empty;
    public string InvoiceValue { get; set; } = string.Empty;
    public string NormalizedTransactionValue { get; set; } = string.Empty;
    public string NormalizedInvoiceValue { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
}

public sealed class BankReconciliationMatchEvidence
{
    public List<BankReconciliationReferenceEvidence> ReferenceMatches { get; set; } = new();
    public decimal TransactionAmount { get; set; }
    public decimal InvoiceRemainingAmount { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal AmountDifference { get; set; }
    public string Currency { get; set; } = "SEK";
    public bool CurrencyMatched { get; set; }
    public List<string> MatchedNameTokens { get; set; } = new();
    public int? DateDifferenceDays { get; set; }
    public List<BankReconciliationEligibilityRule> EligibilityRules { get; set; } = new();
}

public sealed class BankReconciliationEligibilityRule
{
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class BankReconciliationMatchEligibilityResult
{
    public List<BankReconciliationEligibilityRule> Rules { get; set; } = new();
    public bool IsEligible => Rules.All(rule => !string.Equals(rule.Status, "blocked", StringComparison.Ordinal));
    public bool RequiresManualReview => Rules.Any(rule => string.Equals(rule.Status, "warning", StringComparison.Ordinal));
}

public sealed class BankReconciliationConfidence
{
    public string Level { get; set; } = "Låg";
    public int Score { get; set; }
}

public sealed class BankReconciliationRecommendationInvoice
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string? Ocr { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? DueDate { get; set; }
    public bool IsSupplierInvoice { get; set; }
}

public sealed class BankReconciliationRecommendationItem
{
    public BankReconciliationRecommendationInvoice Invoice { get; set; } = new();
    public BankReconciliationConfidence Confidence { get; set; } = new();
    public string RuleLabel { get; set; } = string.Empty;
    public string RuleHelp { get; set; } = string.Empty;
    public string RuleKey { get; set; } = string.Empty;
    public bool RequiresManualConfirmation { get; set; }
    public string? ManualConfirmationReason { get; set; }
    public BankReconciliationMatchEvidence Evidence { get; set; } = new();
}

public sealed class BankReconciliationAutoMatchResult
{
    public List<BankReconciliationSavedMatch> Matches { get; set; } = new();
}

public sealed class BankReconciliationAiSuggestionRequest
{
    public Guid CompanyId { get; set; }
    public string StateKey { get; set; } = string.Empty;
    public string? RequestedByUserId { get; set; }
    public BankReconciliationTransactionCandidate Transaction { get; set; } = new();
    public List<BankReconciliationRecommendationItem> RuleCandidates { get; set; } = new();
}

public sealed class BankReconciliationAiSuggestionCandidate
{
    public string InvoiceId { get; set; } = string.Empty;
    public decimal MatchedAmount { get; set; }
    public string Currency { get; set; } = "SEK";
    public int ConfidenceScore { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public bool RequiresManualConfirmation { get; set; } = true;
    public string? VerificationStatus { get; set; }
    public List<string> VerificationErrors { get; set; } = new();
}

public sealed class BankReconciliationAiSuggestionResult
{
    public bool Enabled { get; set; }
    public string Status { get; set; } = "disabled";
    public string PromptVersion { get; set; } = string.Empty;
    public string InputHash { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<BankReconciliationAiSuggestionCandidate> Suggestions { get; set; } = new();
}

public sealed class BankReconciliationAiSuggestionVerificationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public BankReconciliationAiSuggestionCandidate Candidate { get; set; } = new();
}
