// Defines the durable portal-database records used by bank reconciliation.
namespace WebApp.Models.Integration.BankReconciliation;

public sealed class BankReconciliationStateRecord
{
    public Guid CompanyId { get; set; }
    public string StateKeyHash { get; set; } = string.Empty;
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BankReconciliationImportRegistryRecord
{
    public Guid CompanyId { get; set; }
    public string AccountFingerprint { get; set; } = string.Empty;
    public int Version { get; set; }
    public string RegistryJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BankReconciliationCodingRuleRecord
{
    public Guid CompanyId { get; set; }
    public string BankAccountKeyHash { get; set; } = string.Empty;
    public int Version { get; set; }
    public string RuleSetJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
