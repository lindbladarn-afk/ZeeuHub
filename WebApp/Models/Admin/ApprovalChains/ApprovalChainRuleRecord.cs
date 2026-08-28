namespace WebApp.Models.Admin.ApprovalChains;

// Portal-side mirror of Jeeves q_zu_approval_chains used while moving approval rules into the hub.
public sealed class ApprovalChainRuleRecord
{
    public short ForetagKod { get; set; }
    public int SqlIdentity { get; set; }
    public int FlowId { get; set; }
    public string CurrentApproverPersSign { get; set; } = string.Empty;
    public string NextApproverPersSign { get; set; } = string.Empty;
    public short? PurchaseOrderType { get; set; }
    public short? SalesOrderType { get; set; }
    public int? PriceListId { get; set; }
    public decimal Limit { get; set; }
    public decimal NegativeLimit { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string PersSign { get; set; } = string.Empty;
    public string RowCreatedBy { get; set; } = string.Empty;
    public DateTime RowCreatedAt { get; set; }
    public string? RowUpdatedBy { get; set; }
    public DateTime? RowUpdatedAt { get; set; }
    public string? IsDefaultRaw { get; set; }
    public string? SendMailRaw { get; set; }

    public bool IsDefault => string.Equals(IsDefaultRaw, "1", StringComparison.OrdinalIgnoreCase);
    public bool SendMail => string.Equals(SendMailRaw, "1", StringComparison.OrdinalIgnoreCase);
}
