using System.Text.Json.Serialization;
using WebApp.Models.Invoices;

namespace WebApp.Models.Integration;

// Workspace models describe the active bank reconciliation source and invoice payloads.
public sealed class BankReconciliationSourceContext
{
    public bool IsDemoMode { get; set; }
    public bool HasSource { get; set; }
    public string? StateKey { get; set; }
    public string? DemoScenarioKey { get; set; }
    public string? SourceLabel { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }
    public string? BankAccountKey { get; set; }
    public string? BankAccountLabel { get; set; }
    public string? BankAccountIban { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountOwner { get; set; }
    public string? BankAccountBic { get; set; }
    public string? ErrorMessage { get; set; }
    public List<BankReconciliationParsedTransaction> Transactions { get; set; } = new();
}

public sealed class BankReconciliationInvoiceCandidateResult
{
    public List<InvoiceItem> Invoices { get; set; } = new();
    public int TotalCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class BankReconciliationInvoicePayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("ocr")]
    public string? Ocr { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "SEK";

    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("isDemo")]
    public bool IsDemo { get; set; }
}
