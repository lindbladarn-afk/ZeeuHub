using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Verifies the accounting rules that block unsafe transaction-to-invoice comparisons.
public sealed class BankReconciliationMatchEligibilityServiceTests
{
    private readonly BankReconciliationMatchEligibilityService _service = new();

    [Fact]
    public void Evaluate_BookedCustomerReceiptWithMatchingCurrency_IsEligible()
    {
        var result = _service.Evaluate(CreateCustomerReceipt(), CreateCustomerInvoice());

        Assert.True(result.IsEligible);
        Assert.All(result.Rules, rule => Assert.NotEqual("blocked", rule.Status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PDNG")]
    public void Evaluate_NonBookedTransaction_IsBlocked(string? status)
    {
        var transaction = CreateCustomerReceipt();
        transaction.EntryStatus = status;

        var result = _service.Evaluate(transaction, CreateCustomerInvoice());

        Assert.False(result.IsEligible);
        Assert.Contains(result.Rules, rule => rule.Code == "booking-status" && rule.Status == "blocked");
    }

    [Fact]
    public void Evaluate_CustomerInvoiceAgainstDebit_IsBlocked()
    {
        var transaction = CreateCustomerReceipt();
        transaction.Direction = "DBIT";
        transaction.Amount = -100m;

        var result = _service.Evaluate(transaction, CreateCustomerInvoice());

        Assert.False(result.IsEligible);
        Assert.Contains(result.Rules, rule => rule.Code == "accounting-direction" && rule.Status == "blocked");
    }

    [Fact]
    public void Evaluate_CurrencyMismatch_IsBlocked()
    {
        var invoice = CreateCustomerInvoice();
        invoice.Currency = "EUR";

        var result = _service.Evaluate(CreateCustomerReceipt(), invoice);

        Assert.False(result.IsEligible);
        Assert.Contains(result.Rules, rule => rule.Code == "currency" && rule.Status == "blocked");
    }

    [Fact]
    public void Evaluate_InvalidNumericOcr_RequiresManualReview()
    {
        var invoice = CreateCustomerInvoice();
        invoice.Ocr = "462166597";

        var result = _service.Evaluate(CreateCustomerReceipt(), invoice);

        Assert.True(result.IsEligible);
        Assert.True(result.RequiresManualReview);
        Assert.Contains(result.Rules, rule => rule.Code == "ocr-check-digit" && rule.Status == "warning");
    }

    private static BankReconciliationTransactionCandidate CreateCustomerReceipt()
        => new()
        {
            TransactionId = "TX-1",
            EntryStatus = "BOOK",
            Direction = "CRDT",
            Date = "2026-05-12",
            Amount = 100m,
            Currency = "SEK"
        };

    private static InvoiceItem CreateCustomerInvoice()
        => new()
        {
            InvoiceNo = "1001",
            Ocr = "462166596",
            Currency = "SEK",
            AmountSek = 100m,
            RemainingAmount = 100m,
            DueDate = new DateTime(2026, 5, 12)
        };
}
