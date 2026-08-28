using System.Text.Json.Serialization;

namespace WebApp.Models.Integration;

// Invoice page query models define the JSON contract for bank reconciliation invoice paging.
public sealed class BankReconciliationInvoicePageQueryResult
{
    [JsonPropertyName("items")]
    public List<BankReconciliationInvoicePayload> Items { get; set; } = new();

    [JsonPropertyName("activeTab")]
    public string? ActiveTab { get; set; }

    [JsonPropertyName("usesHistoricalFactSource")]
    public bool UsesHistoricalFactSource { get; set; }

    [JsonPropertyName("dataSourceNotice")]
    public string? DataSourceNotice { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
