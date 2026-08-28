namespace WebApp.Models.Integration;

public sealed class BankReconciliationDemoData
{
    public List<BankReconciliationDemoTransaction> Transactions { get; set; } = new();
    public List<BankReconciliationDemoInvoice> Invoices { get; set; } = new();
}

public sealed class BankReconciliationDemoScenario
{
    public string Key { get; set; } = "overview";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BankReconciliationDemoData Data { get; set; } = new();
    public List<BankReconciliationSavedMatch> SeedMatches { get; set; } = new();
}

public sealed class BankReconciliationDemoScenarioOption
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class BankReconciliationDemoTransaction
{
    public string Id { get; set; } = string.Empty;
    public string? Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? Reference { get; set; }
    public string? EndToEndId { get; set; }
    public string? DebtorName { get; set; }
    public string? Remittance { get; set; }
}

public sealed class BankReconciliationDemoInvoice
{
    public string Id { get; set; } = string.Empty;
    public string? InvoiceNo { get; set; }
    public string? Ocr { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";
    public string? DueDate { get; set; }
}
