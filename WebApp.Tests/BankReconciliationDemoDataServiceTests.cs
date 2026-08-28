using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Demo data tests keep scenario fixtures aligned with the workflows they are meant to demonstrate.
public sealed class BankReconciliationDemoDataServiceTests
{
    [Fact]
    public async Task LoadAsync_ReadsBundledDemoFiles()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = webAppRoot,
            ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
        };
        var service = new BankReconciliationDemoDataService(environment);

        var result = await service.LoadAsync();

        Assert.NotEmpty(result.Transactions);
        Assert.NotEmpty(result.Invoices);
        Assert.Contains(result.Transactions, x => x.Id == "TX-001");
        Assert.Contains(result.Invoices, x => x.InvoiceNo == "1001");
        Assert.Contains(result.Invoices, x => x.InvoiceNo == "1015" && x.Amount == 12500m);
    }

    [Fact]
    public async Task LoadScenarioAsync_PartialPayments_IncludesSeedMatchesAndScenarioMetadata()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = webAppRoot,
            ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
        };
        var service = new BankReconciliationDemoDataService(environment);

        var result = await service.LoadScenarioAsync("partial-payments");

        Assert.Equal("partial-payments", result.Key);
        Assert.Equal("Delbetalningar", result.Title);
        Assert.NotEmpty(result.SeedMatches);
        Assert.Contains(result.Data.Transactions, x => x.Id == "TX-P001");
        Assert.Contains(result.SeedMatches, x => x.TransactionId == "TX-P001" && x.InvoiceId == "INV-1001");

        var suggestions = BuildPaymentBundleSuggestions(result);
        Assert.Contains(suggestions, suggestion =>
            suggestion.InvoiceNo == "1011" &&
            suggestion.Allocations.Count == 2 &&
            suggestion.TotalMatchedAmount == 10000m &&
            suggestion.AmountDifference == 0m);
        Assert.Contains(suggestions, suggestion =>
            suggestion.InvoiceNo == "1012" &&
            suggestion.Allocations.Count == 3 &&
            suggestion.TotalMatchedAmount == 7250m &&
            suggestion.AmountDifference == 0m);
        Assert.Contains(suggestions, suggestion =>
            suggestion.InvoiceNo == "1013" &&
            suggestion.Allocations.Count == 2 &&
            suggestion.TotalMatchedAmount == 4999.50m &&
            suggestion.AmountDifference == 0.50m);
    }

    [Fact]
    public async Task LoadScenarioAsync_AiCamtLab_IncludesKnownMatchesAndNoMatchControl()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = webAppRoot,
            ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
        };
        var service = new BankReconciliationDemoDataService(environment);

        var result = await service.LoadScenarioAsync("ai-camt-lab");

        Assert.Equal("ai-camt-lab", result.Key);
        Assert.Contains(result.Data.Transactions, x => x.Id == "TX-AI001" && x.Reference == "873550016");
        Assert.Contains(result.Data.Invoices, x => x.Id == "AI-INV-81001" && x.Ocr == "873550016");
        Assert.Contains(result.Data.Transactions, x => x.Id == "TX-AI007" && x.Reference == "NO-MATCH-001");
        Assert.DoesNotContain(result.Data.Invoices, x => x.Ocr == "NO-MATCH-001");
        Assert.Contains(result.Data.Transactions, x => x.Id == "TX-AI014" && x.Amount == 2999.50m && x.Reference == "992000137");
        Assert.Contains(result.Data.Invoices, x => x.Id == "AI-INV-82003" && x.Amount == 5000m);

        var autoMatches = new BankReconciliationMatchingService().BuildAutoMatches(
            BuildTransactionCandidates(result),
            BuildInvoices(result));
        Assert.Equal(2, autoMatches.Matches.Count);
        Assert.Contains(autoMatches.Matches, match => match.TransactionId == "TX-AI001" && match.InvoiceId == "81001");
        Assert.Contains(autoMatches.Matches, match => match.TransactionId == "TX-AI004" && match.InvoiceId == "81004");

        var suggestions = BuildPaymentBundleSuggestions(result);
        Assert.Contains(suggestions, suggestion => suggestion.InvoiceNo == "82001" && suggestion.Allocations.Count == 2);
        Assert.Contains(suggestions, suggestion => suggestion.InvoiceNo == "82002" && suggestion.Allocations.Count == 3);
        Assert.Contains(suggestions, suggestion => suggestion.InvoiceNo == "82003" && suggestion.AmountDifference == 0.50m);
    }

    private static IReadOnlyList<BankReconciliationPaymentBundleSuggestion> BuildPaymentBundleSuggestions(
        BankReconciliationDemoScenario scenario)
    {
        var matcher = new BankReconciliationPaymentBundleMatcher(
            new BankReconciliationMatchingService(),
            Options.Create(new BankReconciliationPaymentBundleOptions()));

        return matcher.BuildSuggestions(BuildTransactionCandidates(scenario), BuildInvoices(scenario), scenario.SeedMatches);
    }

    private static IReadOnlyList<BankReconciliationTransactionCandidate> BuildTransactionCandidates(
        BankReconciliationDemoScenario scenario)
        => scenario.Data.Transactions.Select(transaction => new BankReconciliationTransactionCandidate
        {
            TransactionId = transaction.Id,
            Date = transaction.Date,
            EntryStatus = "BOOK",
            Direction = transaction.Amount < 0m ? "DBIT" : "CRDT",
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Reference = transaction.Reference,
            EndToEndId = transaction.EndToEndId,
            DebtorName = transaction.DebtorName,
            Remittance = transaction.Remittance,
            ResolvedCodingTypeKey = "bankinbetalningar"
        }).ToList();

    private static IReadOnlyList<InvoiceItem> BuildInvoices(BankReconciliationDemoScenario scenario)
        => scenario.Data.Invoices.Select(invoice => new InvoiceItem
        {
            InvoiceNo = invoice.InvoiceNo ?? invoice.Id,
            Ocr = invoice.Ocr ?? string.Empty,
            Customer = invoice.CustomerName ?? string.Empty,
            AmountSek = invoice.Amount,
            RemainingAmount = invoice.Amount,
            Currency = invoice.Currency,
            DueDate = DateTime.TryParse(invoice.DueDate, out var dueDate) ? dueDate : DateTime.Today
        }).ToList();
}
