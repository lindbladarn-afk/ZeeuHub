namespace WebApp.Models.Integration;

// Parsed bank reconciliation models keep CAMT source signals explicit and testable before matching.
public sealed class BankReconciliationParsedDocument
{
    public List<BankReconciliationParsedStatement> Statements { get; set; } = new();
    public IReadOnlyList<BankReconciliationParsedTransaction> Transactions
        => Statements.SelectMany(statement => statement.Entries).SelectMany(entry => entry.Transactions).ToList();
}

public sealed class BankReconciliationParsedStatement
{
    public string? StatementId { get; set; }
    public string? ElectronicSequenceNumber { get; set; }
    public string? LegalSequenceNumber { get; set; }
    public string? CreatedAt { get; set; }
    public string? AccountIban { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountCurrency { get; set; }
    public string? AccountOwner { get; set; }
    public string? BankBic { get; set; }
    public List<BankReconciliationParsedBalance> Balances { get; set; } = new();
    public List<BankReconciliationParsedEntry> Entries { get; set; } = new();
}

public sealed class BankReconciliationParsedBalance
{
    public string? TypeCode { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Direction { get; set; }
    public string? Date { get; set; }
}

public sealed class BankReconciliationParsedEntry
{
    public string? EntryReference { get; set; }
    public string? AccountServiceReference { get; set; }
    public string? Status { get; set; }
    public string? Direction { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? BookingDate { get; set; }
    public string? ValueDate { get; set; }
    public string? DomainCode { get; set; }
    public string? FamilyCode { get; set; }
    public string? SubFamilyCode { get; set; }
    public List<BankReconciliationParsedBatch> Batches { get; set; } = new();
    public List<BankReconciliationParsedTransaction> Transactions { get; set; } = new();
}

public sealed class BankReconciliationParsedBatch
{
    public string? MessageId { get; set; }
    public string? PaymentInformationId { get; set; }
    public int? DeclaredTransactionCount { get; set; }
}

public sealed class BankReconciliationParsedTransaction
{
    public string Id { get; set; } = string.Empty;
    public string? LegacyId { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public string DuplicateFingerprint { get; set; } = string.Empty;
    public string? StatementId { get; set; }
    public string? StatementAccountIban { get; set; }
    public string? StatementAccountNumber { get; set; }
    public string? StatementAccountOwner { get; set; }
    public string? StatementBankBic { get; set; }
    public string? Date { get; set; }
    public string? ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? Reference { get; set; }
    public string? EndToEndId { get; set; }
    public string? TxId { get; set; }
    public string? AcctSvcrRef { get; set; }
    public string? EntryReference { get; set; }
    public string? EntryAccountServiceReference { get; set; }
    public string? EntryStatus { get; set; }
    public string? BatchMessageId { get; set; }
    public string? BatchPaymentInformationId { get; set; }
    public string? InstructionId { get; set; }
    public string? PaymentInformationId { get; set; }
    public string? DebtorName { get; set; }
    public string? DebtorOrganizationId { get; set; }
    public string? DebtorAccountId { get; set; }
    public string? CreditorName { get; set; }
    public string? CreditorOrganizationId { get; set; }
    public string? CreditorAccountId { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Remittance { get; set; }
    public string? Direction { get; set; }
    public string? Domn { get; set; }
    public string? Fmly { get; set; }
    public string? SubFmly { get; set; }
    public string? ScorType { get; set; }
    public BankReconciliationTransactionClassification Classification { get; set; } = new();
    public string? Group { get; set; }
    public string? ClassificationRule { get; set; }
    public string? MatchedInvoiceId { get; set; }
    public string? MatchType { get; set; }
    public string? MatchRule { get; set; }
    public decimal? MatchedAmount { get; set; }
    public List<BankReconciliationReferenceCandidate> ReferenceCandidates { get; set; } = new();
    public List<BankReconciliationParsedRemittanceAllocation> RemittanceAllocations { get; set; } = new();
    public List<BankReconciliationParsedAllocation> Allocations { get; set; } = new();
}

public sealed class BankReconciliationParsedRemittanceAllocation
{
    public string? DocumentTypeCode { get; set; }
    public string? DocumentNumber { get; set; }
    public string? CreditorReference { get; set; }
    public decimal? RemittedAmount { get; set; }
    public decimal? CreditNoteAmount { get; set; }
    public string? Currency { get; set; }
    public string? AdditionalInformation { get; set; }
}

public sealed class BankReconciliationReferenceCandidate
{
    public string SourcePath { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public string CandidateType { get; set; } = "reference";
}

public sealed class BankReconciliationParsedAllocation
{
    public string AllocationId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string? MatchType { get; set; }
    public string? MatchRule { get; set; }
    public decimal MatchedAmount { get; set; }
    public string Currency { get; set; } = "SEK";
}
