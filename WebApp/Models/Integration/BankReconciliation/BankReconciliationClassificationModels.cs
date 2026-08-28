namespace WebApp.Models.Integration;

// Transaction classifications keep the new bank reconciliation metrics separate from the legacy UI grouping.
public sealed class BankReconciliationTransactionClassification
{
    public string TypeKey { get; set; } = "def";
    public string TypeLabel { get; set; } = "DEF";
    public string RuleKey { get; set; } = "fallback";
    public string RuleLabel { get; set; } = "Standard";
    public string? SuggestedAccount { get; set; }
    public string? SuggestedCostCenter { get; set; }
    public bool IsDefault { get; set; } = true;
    public string LegacyGroup { get; set; } = "Ovrigt";
    public string LegacyRule { get; set; } = "fallback";
}

// Coding rules keep the persisted per-company bank account matrix separate from the live transaction data.
public sealed class BankReconciliationCodingRuleSet
{
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CompanyId { get; set; } = string.Empty;
    public string BankAccountKey { get; set; } = string.Empty;
    public string? BankAccountLabel { get; set; }
    public List<BankReconciliationCodingRuleRow> Rows { get; set; } = new();
    public List<BankReconciliationCodingRuleAuditEntry> AuditTrail { get; set; } = new();
}

public sealed class BankReconciliationCodingRuleRow
{
    public string RowId { get; set; } = Guid.NewGuid().ToString("N");
    public string TypeKey { get; set; } = "def";
    public string TypeLabel { get; set; } = "DEF";
    public string RuleLabel { get; set; } = "Standard";
    public string SourceBankAccountKey { get; set; } = string.Empty;
    public string? SuggestedAccount { get; set; }
    public string? SuggestedCostCenter { get; set; }
    public string? Account { get; set; }
    public string? CostCenter { get; set; }
    public bool IsDefault { get; set; }
    public bool IsInherited { get; set; }
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class BankReconciliationCodingRuleAuditEntry
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ActionType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? BankAccountKey { get; set; }
    public int RowCount { get; set; }
}

public sealed class BankReconciliationCodingRuleSaveRequest
{
    public string BankAccountKey { get; set; } = string.Empty;
    public string? BankAccountLabel { get; set; }
    public int? ExpectedVersion { get; set; }
    public List<BankReconciliationCodingRuleRow> Rows { get; set; } = new();
}
