using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Allocation balance tests protect residual amounts used by incremental auto-match and payment bundles.
public sealed class BankReconciliationAllocationBalanceTests
{
    [Fact]
    public void BuildAvailableTransactions_SubtractsAllocationsAndPreservesDebitDirection()
    {
        var result = BankReconciliationAllocationBalance.BuildAvailableTransactions(
            new[]
            {
                new BankReconciliationTransactionCandidate
                {
                    TransactionId = "TX-1",
                    Amount = -100m,
                    Currency = "SEK"
                }
            },
            new[]
            {
                new BankReconciliationSavedMatch { TransactionId = "TX-1", InvoiceId = "INV-OTHER", MatchedAmount = 35m }
            });

        Assert.Equal(-65m, Assert.Single(result).Amount);
    }

    [Fact]
    public void BuildAvailableInvoices_SubtractsExistingAllocationsWithoutMutatingSource()
    {
        var invoice = new InvoiceItem
        {
            InvoiceNo = "INV-1",
            AmountSek = 100m,
            RemainingAmount = 100m
        };

        var result = BankReconciliationAllocationBalance.BuildAvailableInvoices(
            new[] { invoice },
            new[]
            {
                new BankReconciliationSavedMatch { TransactionId = "TX-1", InvoiceId = "INV-1", MatchedAmount = 40m }
            });

        Assert.Equal(60m, Assert.Single(result).RemainingAmount);
        Assert.Equal(100m, invoice.RemainingAmount);
    }
}
