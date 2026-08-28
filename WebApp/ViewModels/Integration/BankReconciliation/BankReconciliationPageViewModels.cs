using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.Validation;
using WebApp.ViewModels.Shared;

namespace WebApp.ViewModels.Integration.BankReconciliation;

// Bank reconciliation page models define the Razor contract for the reconciliation workspace.
public sealed class BankReconciliationPageViewModel
{
    public string TransactionsJson { get; set; } = "[]";
    public string InvoicesJson { get; set; } = "[]";
    public ModuleBannerViewModel? RuntimeBanner { get; set; }
    public bool IsDemoMode { get; set; }
    public string DemoScenarioKey { get; set; } = "overview";
    public IReadOnlyList<BankReconciliationDemoScenarioOption> DemoScenarios { get; set; } = Array.Empty<BankReconciliationDemoScenarioOption>();
    public string BankAccountKey { get; set; } = "default";
    public string BankAccountLabel { get; set; } = "Okänt bankkonto";
    public string ActiveCompanyName { get; set; } = string.Empty;
    public string CodingRulesJson { get; set; } = "[]";
    public int CodingRulesVersion { get; set; }
    public string? UploadError { get; set; }
    public string? UploadInfo { get; set; }
    public string? StatusMessage { get; set; }
    public string StatusTone { get; set; } = "info";
    public string? LatestFileName { get; set; }
    public DateTime? LatestUploadedAt { get; set; }
    public bool HasUploadedFile { get; set; }
    public BankReconciliationCamtValidationResult? ValidationReport { get; set; }
}

// Invoice detail page model defines the Razor contract for invoice-focused reconciliation details.
public sealed class BankReconciliationInvoicePageViewModel
{
    public string TransactionsJson { get; set; } = "[]";
    public string InvoicesJson { get; set; } = "[]";
    public string InvoiceId { get; set; } = string.Empty;
    public ModuleBannerViewModel? RuntimeBanner { get; set; }
    public bool HasUploadedFile { get; set; }
    public bool IsDemoMode { get; set; }
    public string LatestFileName { get; set; } = string.Empty;
    public DateTime? LatestUploadedAt { get; set; }
}
