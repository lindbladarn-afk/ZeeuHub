using System.Text.Json.Serialization;

namespace WebApp.Models.Integration;

// Transaction page models keep the bank reconciliation JSON contract stable outside the controller.
public sealed class BankReconciliationTransactionPageResult
{
    [JsonPropertyName("items")]
    public IReadOnlyList<BankReconciliationTransactionItem> Items { get; set; } = Array.Empty<BankReconciliationTransactionItem>();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("totals")]
    public BankReconciliationTransactionTotals Totals { get; set; } = new();

    [JsonPropertyName("classificationSummary")]
    public IReadOnlyList<BankReconciliationClassificationSummaryItem> ClassificationSummary { get; set; } = Array.Empty<BankReconciliationClassificationSummaryItem>();

    [JsonPropertyName("groupCounts")]
    public BankReconciliationTransactionGroupCounts GroupCounts { get; set; } = new();

    [JsonPropertyName("manualReviewItems")]
    public IReadOnlyList<BankReconciliationTransactionItem> ManualReviewItems { get; set; } = Array.Empty<BankReconciliationTransactionItem>();

    [JsonPropertyName("autoResultItems")]
    public IReadOnlyList<BankReconciliationTransactionItem> AutoResultItems { get; set; } = Array.Empty<BankReconciliationTransactionItem>();

    [JsonPropertyName("summary")]
    public BankReconciliationSummary Summary { get; set; } = new();

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public sealed class BankReconciliationTransactionTotals
{
    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("matched")]
    public decimal Matched { get; set; }

    [JsonPropertyName("unmatched")]
    public decimal Unmatched { get; set; }
}

public sealed class BankReconciliationSummary
{
    [JsonPropertyName("matched")]
    public int Matched { get; set; }

    [JsonPropertyName("review")]
    public int Review { get; set; }

    [JsonPropertyName("unmatched")]
    public int Unmatched { get; set; }
}

public sealed class BankReconciliationClassificationSummaryItem
{
    public string Key { get; set; } = "def";
    public string Label { get; set; } = "DEF";
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public int DefaultCount { get; set; }
    public string RuleLabel { get; set; } = "Standard";
    public string? SuggestedAccount { get; set; }
    public string? SuggestedCostCenter { get; set; }
    public bool IsDefault { get; set; } = true;
}

public sealed class BankReconciliationTransactionGroupCounts
{
    public int All { get; set; }
    public int Kundinbetalningar { get; set; }
    public int Leverantorsutbetalningar { get; set; }
    public int Ovrigt { get; set; }
}

public sealed class BankReconciliationTransactionItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("valueDate")]
    public string? ValueDate { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "SEK";

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("endToEndId")]
    public string? EndToEndId { get; set; }

    [JsonPropertyName("txId")]
    public string? TxId { get; set; }

    [JsonPropertyName("acctSvcrRef")]
    public string? AcctSvcrRef { get; set; }

    [JsonPropertyName("statementId")]
    public string? StatementId { get; set; }

    [JsonPropertyName("statementAccountIban")]
    public string? StatementAccountIban { get; set; }

    [JsonPropertyName("statementAccountNumber")]
    public string? StatementAccountNumber { get; set; }

    [JsonPropertyName("statementAccountOwner")]
    public string? StatementAccountOwner { get; set; }

    [JsonPropertyName("statementBankBic")]
    public string? StatementBankBic { get; set; }

    [JsonPropertyName("debtorName")]
    public string? DebtorName { get; set; }

    [JsonPropertyName("remittance")]
    public string? Remittance { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("domn")]
    public string? Domn { get; set; }

    [JsonPropertyName("fmly")]
    public string? Fmly { get; set; }

    [JsonPropertyName("subFmly")]
    public string? SubFmly { get; set; }

    [JsonPropertyName("scorType")]
    public string? ScorType { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("classificationRule")]
    public string? ClassificationRule { get; set; }

    [JsonPropertyName("classification")]
    public BankReconciliationTransactionClassification Classification { get; set; } = new();

    [JsonPropertyName("matchedInvoiceId")]
    public string? MatchedInvoiceId { get; set; }

    [JsonPropertyName("matchType")]
    public string? MatchType { get; set; }

    [JsonPropertyName("matchRule")]
    public string? MatchRule { get; set; }

    [JsonPropertyName("matchedAmount")]
    public decimal? MatchedAmount { get; set; }

    [JsonPropertyName("referenceCandidates")]
    public IReadOnlyList<BankReconciliationReferenceCandidateItem> ReferenceCandidates { get; set; } = Array.Empty<BankReconciliationReferenceCandidateItem>();

    [JsonPropertyName("allocations")]
    public IReadOnlyList<BankReconciliationAllocationItem> Allocations { get; set; } = Array.Empty<BankReconciliationAllocationItem>();
}

public sealed class BankReconciliationReferenceCandidateItem
{
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("rawValue")]
    public string RawValue { get; set; } = string.Empty;

    [JsonPropertyName("normalizedValue")]
    public string NormalizedValue { get; set; } = string.Empty;

    [JsonPropertyName("candidateType")]
    public string CandidateType { get; set; } = "reference";
}

public sealed class BankReconciliationAllocationItem
{
    [JsonPropertyName("allocationId")]
    public string AllocationId { get; set; } = string.Empty;

    [JsonPropertyName("invoiceId")]
    public string InvoiceId { get; set; } = string.Empty;

    [JsonPropertyName("matchType")]
    public string? MatchType { get; set; }

    [JsonPropertyName("matchRule")]
    public string? MatchRule { get; set; }

    [JsonPropertyName("matchedAmount")]
    public decimal MatchedAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "SEK";
}
