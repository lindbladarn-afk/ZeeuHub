using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

public sealed class BankReconciliationMatchingServiceTests
{
    private readonly BankReconciliationMatchingService _service = new();
    private static BankReconciliationMatchingService CreateService(BankReconciliationMatchingOptions options)
        => new(Microsoft.Extensions.Options.Options.Create(options));

    [Fact]
    public void BuildRecommendations_ExactReferenceAndAmount_GivesHighConfidenceWithoutManualConfirmation()
    {
        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Amount = 100m,
            Currency = "SEK",
            Reference = "462166596",
            Remittance = "CINV 462166596",
            DebtorName = "Birgitta Andersson",
            Date = "2025-12-15"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Birgitta Andersson",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.Equal("1001", recommendation.Invoice.InvoiceNo);
        Assert.Equal("Hög", recommendation.Confidence.Level);
        Assert.False(recommendation.RequiresManualConfirmation);
        Assert.Contains("ref-exact", recommendation.RuleKey);
        Assert.Contains("amount-exact", recommendation.RuleKey);
        Assert.Contains(recommendation.Evidence.ReferenceMatches, evidence =>
            evidence.MatchType == "exact" &&
            evidence.TransactionSource == "reference" &&
            evidence.InvoiceSource == "ocr");
        Assert.Equal(0m, recommendation.Evidence.AmountDifference);
        Assert.Contains("BIRGITTA", recommendation.Evidence.MatchedNameTokens);
    }

    [Fact]
    public void BuildRecommendations_SupplierPaymentDebit_UsesAbsoluteTransactionAmount()
    {
        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "DBIT",
            Amount = -1250m,
            Currency = "SEK",
            Reference = "L-1001",
            DebtorName = "Demo Supplier AB",
            Date = "2026-05-17",
            ResolvedCodingTypeKey = "leverantorsbetalning"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "L-1001",
                Ocr = "L-1001",
                Customer = "Demo Supplier AB",
                AmountSek = 1250m,
                RemainingAmount = 1250m,
                DueDate = new DateTime(2026, 5, 17),
                IsSupplierInvoice = true
            }
        };

        var result = _service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.Equal("L-1001", recommendation.Invoice.InvoiceNo);
        Assert.Equal(1250m, recommendation.Evidence.TransactionAmount);
        Assert.False(recommendation.RequiresManualConfirmation);
        Assert.Contains("amount-exact", recommendation.RuleKey);
    }

    [Fact]
    public void BuildAutoMatches_SupplierPaymentDebit_SavesPositiveMatchedAmount()
    {
        var transactions = new List<BankReconciliationTransactionCandidate>
        {
            new()
            {
                TransactionId = "TX-1",
                EntryStatus = "BOOK",
                Direction = "DBIT",
                Date = "2026-05-17",
                Amount = -1250m,
                Currency = "SEK",
                Reference = "L-1001",
                ResolvedCodingTypeKey = "leverantorsbetalning"
            }
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "L-1001",
                Ocr = "L-1001",
                Customer = "Demo Supplier AB",
                AmountSek = 1250m,
                RemainingAmount = 1250m,
                DueDate = new DateTime(2026, 5, 17),
                IsSupplierInvoice = true
            }
        };

        var result = _service.BuildAutoMatches(transactions, invoices);

        var match = Assert.Single(result.Matches);
        Assert.Equal("TX-1", match.TransactionId);
        Assert.Equal("L-1001", match.InvoiceId);
        Assert.Equal(1250m, match.MatchedAmount);
    }

    [Fact]
    public void BuildRecommendations_ToleranceMatch_RequiresManualConfirmation()
    {
        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Amount = 99.50m,
            Currency = "SEK",
            Reference = "462166596",
            DebtorName = "Birgitta Andersson",
            Date = "2025-12-15"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Birgitta Andersson",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.True(recommendation.RequiresManualConfirmation);
        Assert.NotNull(recommendation.ManualConfirmationReason);
    }

    [Fact]
    public void BuildRecommendations_ReferenceCandidateExactMatch_GivesHighConfidence()
    {
        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Amount = 100m,
            Currency = "SEK",
            Reference = null,
            ReferenceCandidates = new List<string> { "CINV 462166596" },
            DebtorName = "Birgitta Andersson",
            Date = "2025-12-15"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Birgitta Andersson",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.Equal("1001", recommendation.Invoice.InvoiceNo);
        Assert.Equal("Hög", recommendation.Confidence.Level);
        Assert.Contains("ref-partial", recommendation.RuleKey);
        Assert.True(recommendation.RequiresManualConfirmation);
        Assert.Contains(recommendation.Evidence.ReferenceMatches, evidence =>
            evidence.MatchType == "partial" &&
            evidence.TransactionSource == "reference-candidate" &&
            evidence.InvoiceSource == "ocr");
    }

    [Fact]
    public void BuildAutoMatches_AmbiguousCandidates_DoesNotAutoMatch()
    {
        var transactions = new List<BankReconciliationTransactionCandidate>
        {
            new()
            {
                TransactionId = "TX-1",
                EntryStatus = "BOOK",
                Direction = "CRDT",
                Date = "2025-12-15",
                Amount = 100m,
                Currency = "SEK",
                Reference = "462166596"
            }
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Kund 1",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            },
            new()
            {
                InvoiceNo = "1002",
                Ocr = "462166596",
                Customer = "Kund 2",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildAutoMatches(transactions, invoices);

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void BuildRecommendations_UsesConfiguredMinimumScore()
    {
        var service = CreateService(new BankReconciliationMatchingOptions
        {
            RecommendationMinimumScore = 65
        });

        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Amount = 100m,
            Currency = "SEK",
            Reference = "missing",
            DebtorName = "Birgitta Andersson",
            Date = "2025-12-15"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Birgitta Andersson",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildRecommendations_SkipsInternalTransferCoding()
    {
        var transaction = new BankReconciliationTransactionCandidate
        {
            TransactionId = "TX-1",
            Amount = 100m,
            Currency = "SEK",
            Reference = "462166596",
            DebtorName = "Birgitta Andersson",
            Date = "2025-12-15",
            ResolvedCodingTypeKey = "overforing-konto"
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Birgitta Andersson",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildRecommendations(transaction, invoices, new Dictionary<string, decimal>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildAutoMatches_SkipsInternalTransferCoding()
    {
        var transactions = new List<BankReconciliationTransactionCandidate>
        {
            new()
            {
                TransactionId = "TX-1",
                Amount = 100m,
                Currency = "SEK",
                Reference = "462166596",
                ResolvedCodingTypeKey = "overforing-konto"
            }
        };

        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "1001",
                Ocr = "462166596",
                Customer = "Kund 1",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2025, 12, 20)
            }
        };

        var result = _service.BuildAutoMatches(transactions, invoices);

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void BuildRecommendations_InternalHubIdIsNeverUsedAsInvoiceReference()
    {
        var transaction = EligibleCustomerReceipt("INV-900", 100m);
        transaction.Reference = null;

        var result = _service.BuildRecommendations(
            transaction,
            new[] { CustomerInvoice("INV-900", 100m) },
            new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.DoesNotContain("ref-exact", recommendation.RuleKey);
        Assert.True(recommendation.RequiresManualConfirmation);
    }

    [Fact]
    public void BuildRecommendations_BankTransactionReferenceTypeIsNotUsedAsInvoiceReference()
    {
        var transaction = EligibleCustomerReceipt("TX-1", 100m);
        transaction.Reference = "INV-900";
        transaction.ReferenceType = "transaction-id";

        var result = _service.BuildRecommendations(
            transaction,
            new[] { CustomerInvoice("INV-900", 100m) },
            new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.DoesNotContain("ref-exact", recommendation.RuleKey);
    }

    [Fact]
    public void BuildRecommendations_PlaceholderReferenceIsIgnored()
    {
        var transaction = EligibleCustomerReceipt("TX-1", 100m);
        transaction.Reference = "NOTPROVIDED";

        var invoice = CustomerInvoice("INV-900", 100m);
        invoice.Ocr = "NOTPROVIDED";

        var result = _service.BuildRecommendations(
            transaction,
            new[] { invoice },
            new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.DoesNotContain("ref-exact", recommendation.RuleKey);
    }

    [Fact]
    public void BuildRecommendations_ShortPartialReferenceIsIgnored()
    {
        var transaction = EligibleCustomerReceipt("TX-1", 100m);
        transaction.Remittance = "Betalning AB12 idag";

        var invoice = CustomerInvoice("AB12", 100m);

        var result = _service.BuildRecommendations(
            transaction,
            new[] { invoice },
            new Dictionary<string, decimal>());

        var recommendation = Assert.Single(result);
        Assert.DoesNotContain("ref-partial", recommendation.RuleKey);
    }

    private static BankReconciliationTransactionCandidate EligibleCustomerReceipt(string id, decimal amount)
        => new()
        {
            TransactionId = id,
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Date = "2026-05-12",
            Amount = amount,
            Currency = "SEK"
        };

    private static InvoiceItem CustomerInvoice(string invoiceNo, decimal amount)
        => new()
        {
            InvoiceNo = invoiceNo,
            Ocr = invoiceNo,
            Customer = "Example Customer AB",
            AmountSek = amount,
            RemainingAmount = amount,
            Currency = "SEK",
            DueDate = new DateTime(2026, 5, 12)
        };
}
