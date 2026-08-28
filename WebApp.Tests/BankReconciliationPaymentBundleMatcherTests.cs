using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Payment bundle matcher tests cover bounded grouping, residual amounts, and currency safety.
public sealed class BankReconciliationPaymentBundleMatcherTests
{
    private readonly BankReconciliationPaymentBundleMatcher _matcher = new(
        new BankReconciliationMatchingService(),
        Options.Create(new BankReconciliationPaymentBundleOptions()));

    [Fact]
    public void BuildSuggestions_TwoReferencedPaymentsEqualInvoice_ReturnsReviewableBundle()
    {
        var result = _matcher.BuildSuggestions(
            new[]
            {
                Transaction("TX-1", 400m, "INV-100"),
                Transaction("TX-2", 600m, "INV-100")
            },
            new[] { Invoice("INV-100", 1000m) },
            Array.Empty<BankReconciliationSavedMatch>());

        var suggestion = Assert.Single(result);
        Assert.Equal("INV-100", suggestion.InvoiceId);
        Assert.Equal(1000m, suggestion.TotalMatchedAmount);
        Assert.Equal(0m, suggestion.AmountDifference);
        Assert.Equal(2, suggestion.Allocations.Count);
        Assert.True(suggestion.RequiresManualConfirmation);
        Assert.Equal("exact-sum+exact-reference", suggestion.ReasonCode);
        Assert.Equal("INV-100", suggestion.InvoiceOcr);
        Assert.Equal("2026-06-30", suggestion.InvoiceDueDate);
        Assert.All(suggestion.Allocations, allocation => Assert.True(allocation.ExactReferenceMatched));

        var firstAllocation = suggestion.Allocations.Single(allocation => allocation.TransactionId == "TX-1");
        Assert.Equal("2026-06-20", firstAllocation.Date);
        Assert.Equal("Example Customer AB", firstAllocation.DebtorName);
        Assert.Equal("INV-100", firstAllocation.Reference);
        Assert.Equal("Delbetalning för INV-100", firstAllocation.Remittance);
    }

    [Fact]
    public void BuildSuggestions_UsesUnallocatedTransactionAndInvoiceAmounts()
    {
        var result = _matcher.BuildSuggestions(
            new[]
            {
                Transaction("TX-1", 500m, "INV-100"),
                Transaction("TX-2", 600m, "INV-100")
            },
            new[] { Invoice("INV-100", 1000m) },
            new[]
            {
                new BankReconciliationSavedMatch
                {
                    TransactionId = "TX-1",
                    InvoiceId = "INV-OTHER",
                    MatchedAmount = 100m
                }
            });

        var suggestion = Assert.Single(result);
        Assert.Equal(400m, suggestion.Allocations.Single(item => item.TransactionId == "TX-1").MatchedAmount);
        Assert.Equal(600m, suggestion.Allocations.Single(item => item.TransactionId == "TX-2").MatchedAmount);
    }

    [Fact]
    public void BuildSuggestions_NonSekTransaction_DoesNotCreateBundle()
    {
        var result = _matcher.BuildSuggestions(
            new[]
            {
                Transaction("TX-1", 400m, "INV-100", "EUR"),
                Transaction("TX-2", 600m, "INV-100")
            },
            new[] { Invoice("INV-100", 1000m) },
            Array.Empty<BankReconciliationSavedMatch>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildSuggestions_NameOnlyEvidence_DoesNotCreateProductionBundle()
    {
        var first = Transaction("TX-1", 400m, "OTHER-1");
        var second = Transaction("TX-2", 600m, "OTHER-2");
        first.DebtorName = "Example Customer AB";
        second.DebtorName = "Example Customer AB";

        var result = _matcher.BuildSuggestions(
            new[] { first, second },
            new[] { Invoice("INV-100", 1000m) },
            Array.Empty<BankReconciliationSavedMatch>());

        Assert.Empty(result);
    }

    private static BankReconciliationTransactionCandidate Transaction(
        string id,
        decimal amount,
        string reference,
        string currency = "SEK")
        => new()
        {
            TransactionId = id,
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Amount = amount,
            Currency = currency,
            Reference = reference,
            Date = "2026-06-20",
            DebtorName = "Example Customer AB",
            Remittance = $"Delbetalning för {reference}",
            ResolvedCodingTypeKey = "bankinbetalningar"
        };

    private static InvoiceItem Invoice(string invoiceNo, decimal remainingAmount)
        => new()
        {
            InvoiceNo = invoiceNo,
            Ocr = invoiceNo,
            Customer = "Example Customer AB",
            AmountSek = remainingAmount,
            RemainingAmount = remainingAmount,
            DueDate = new DateTime(2026, 6, 30)
        };
}
