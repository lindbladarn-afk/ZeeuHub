using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.SupplierInvoices;

namespace WebApp.Tests;

public sealed class BankReconciliationSupplierInvoiceServiceTests
{
    [Fact]
    public async Task GetPaymentCandidatesAsync_MapsSupplierPaymentRowsToInvoiceItems()
    {
        var repository = new FakeSupplierInvoiceRepository(new BankReconciliationSupplierInvoiceResult
        {
            TotalCount = 1,
            Invoices = new[]
            {
                new BankReconciliationSupplierInvoiceRow
                {
                    CompanyCode = 9900,
                    PaymentJournalNumber = 123,
                    InvoiceNumber = "L-1001",
                    PayeeId = "LEV-42",
                    PayeeName = "Demo Supplier AB",
                    PaymentAmount = -1250m,
                    PaymentCurrencyCode = "SEK",
                    InvoiceDate = new DateTime(2026, 5, 10),
                    PreferredPaymentDate = new DateTime(2026, 5, 17),
                    InvoiceAmount = 1250m,
                    InvoiceCurrencyCode = "SEK"
                }
            }
        });
        var service = new BankReconciliationSupplierInvoiceService(repository);

        var result = await service.GetPaymentCandidatesAsync("Server=.;Database=Jeeves;", new BankReconciliationSupplierInvoiceQuery());

        var invoice = Assert.Single(result.Invoices);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("L-1001", invoice.InvoiceNo);
        Assert.Equal("Demo Supplier AB", invoice.Customer);
        Assert.Equal("LEV-42", invoice.SalesPerson);
        Assert.Equal(1250m, invoice.AmountSek);
        Assert.Equal(1250m, invoice.RemainingAmount);
        Assert.Equal("L-1001", invoice.Ocr);
        Assert.Equal("SEK", invoice.Currency);
        Assert.True(invoice.IsSupplierInvoice);
        Assert.False(invoice.IsPaid);
    }

    private sealed class FakeSupplierInvoiceRepository : IBankReconciliationSupplierInvoiceRepository
    {
        private readonly BankReconciliationSupplierInvoiceResult _result;

        public FakeSupplierInvoiceRepository(BankReconciliationSupplierInvoiceResult result)
        {
            _result = result;
        }

        public Task<BankReconciliationSupplierInvoiceResult> GetPaymentCandidatesAsync(
            string connectionString,
            BankReconciliationSupplierInvoiceQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}
