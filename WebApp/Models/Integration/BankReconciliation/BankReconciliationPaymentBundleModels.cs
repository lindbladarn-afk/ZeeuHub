using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Integration;

// Payment bundle models describe reviewed many-to-one allocations without changing persisted match rows.
public sealed class BankReconciliationPaymentBundleSuggestion
{
    public string BundleId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string? InvoiceOcr { get; set; }
    public string? CustomerName { get; set; }
    public string? InvoiceDueDate { get; set; }
    public decimal InvoiceRemainingAmount { get; set; }
    public decimal TotalMatchedAmount { get; set; }
    public decimal AmountDifference { get; set; }
    public string Currency { get; set; } = "SEK";
    public int ConfidenceScore { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public bool RequiresManualConfirmation { get; set; } = true;
    public List<BankReconciliationPaymentBundleAllocation> Allocations { get; set; } = new();
}

public sealed class BankReconciliationPaymentBundleAllocation
{
    public string TransactionId { get; set; } = string.Empty;
    public string? Date { get; set; }
    public string? DebtorName { get; set; }
    public string? Reference { get; set; }
    public string? Remittance { get; set; }
    public decimal MatchedAmount { get; set; }
    public string Currency { get; set; } = "SEK";
    public int EvidenceScore { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public bool ExactReferenceMatched { get; set; }
}

public sealed class BankReconciliationPaymentBundleQueryResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int Version { get; set; }
    public List<BankReconciliationPaymentBundleSuggestion> Suggestions { get; set; } = new();
    public List<BankReconciliationManualPaymentTransaction> AvailableTransactions { get; set; } = new();
    public List<BankReconciliationManualPaymentInvoice> AvailableInvoices { get; set; } = new();
}

public sealed class BankReconciliationConfirmPaymentBundleRequest
{
    [Required]
    [StringLength(128)]
    public string BundleId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int? ExpectedVersion { get; set; }
}

public sealed class BankReconciliationConfirmManualPaymentBundleRequest
{
    [Required]
    [StringLength(128)]
    public string InvoiceId { get; set; } = string.Empty;

    [MinLength(2)]
    [MaxLength(20)]
    public List<string> TransactionIds { get; set; } = new();

    [Range(0, int.MaxValue)]
    public int? ExpectedVersion { get; set; }
}

public sealed class BankReconciliationManualPaymentTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string? Date { get; set; }
    public string? DebtorName { get; set; }
    public string? Reference { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "SEK";
}

public sealed class BankReconciliationManualPaymentInvoice
{
    public string InvoiceId { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string? Ocr { get; set; }
    public string? CustomerName { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "SEK";
}

public sealed class BankReconciliationPaymentBundleCommandResult
{
    public bool Success { get; set; }
    public bool Conflict { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Version { get; set; }
    public int? CurrentVersion { get; set; }
    public List<BankReconciliationSavedMatch> Matches { get; set; } = new();
}
