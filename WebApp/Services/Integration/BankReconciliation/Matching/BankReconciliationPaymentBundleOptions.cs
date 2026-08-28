namespace WebApp.Services.Integration.BankReconciliation;

// Payment bundle options cap search cost and define conservative production matching thresholds.
public sealed class BankReconciliationPaymentBundleOptions
{
    public const string SectionName = "BankReconciliation:PaymentBundles";

    public bool Enabled { get; set; } = true;
    public int MaxCandidateTransactionsPerInvoice { get; set; } = 12;
    public int MaxTransactionsPerBundle { get; set; } = 8;
    public int MaxSuggestions { get; set; } = 10;
    public int MinimumTransactionEvidenceScore { get; set; } = 60;
    public decimal AmountTolerance { get; set; } = 1m;
}
