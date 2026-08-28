// Verifies bank reconciliation invoice candidate loading keeps internal errors out of user-facing results.
using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Helpers;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.SupplierInvoices;
using WebApp.Services.Invoices;

namespace WebApp.Tests;

public sealed class BankReconciliationInvoiceCandidateServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsSafeErrorMessage_WhenInvoiceLookupFails()
    {
        var logger = new CapturingLogger<BankReconciliationInvoiceCandidateService>();
        var service = new BankReconciliationInvoiceCandidateService(
            new ThrowingInvoicesService(),
            new SuccessfulRuntimeContextService(),
            new FakeDemoDataService(),
            new FakeSupplierInvoiceService(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger);

        var result = await service.LoadAsync(
            isDemoMode: false,
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            CancellationToken.None);

        Assert.Contains("Referens:", result.ErrorMessage);
        Assert.DoesNotContain("authorization=secret-value", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("BankReconciliationInvoiceCandidateService failed", errorLog.Message);
        Assert.Contains("SupportId=", errorLog.Message);
    }

    [Fact]
    public async Task LoadAsync_HidesRuntimeContextErrorAndLogsSanitizedDiagnostic()
    {
        var logger = new CapturingLogger<BankReconciliationInvoiceCandidateService>();
        var service = new BankReconciliationInvoiceCandidateService(
            new ThrowingInvoicesService(),
            new FailingRuntimeContextService(),
            new FakeDemoDataService(),
            new FakeSupplierInvoiceService(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger);

        var result = await service.LoadAsync(
            isDemoMode: false,
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            CancellationToken.None);

        Assert.Contains("Referens:", result.ErrorMessage ?? string.Empty);
        Assert.DoesNotContain("runtime context", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization=secret-value", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("runtime context failure", errorLog.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization=secret-value", errorLog.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCombinedPageAsync_MergesBoundedDatabasePagesInStableOrder()
    {
        var customerService = new PagingInvoicesService();
        var supplierService = new PagingSupplierInvoiceService();
        var service = new BankReconciliationInvoiceCandidateService(
            customerService,
            new SuccessfulRuntimeContextService(),
            new FakeDemoDataService(),
            supplierService,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoiceCandidateService>.Instance);

        var result = await service.LoadCombinedPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            page: 2,
            pageSize: 2,
            CancellationToken.None);

        Assert.Equal(4, customerService.RequestedPageSize);
        Assert.Equal(4, supplierService.RequestedPageSize);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(new[] { "C-2", "S-2" }, result.Invoices.Select(item => item.InvoiceNo));
    }

    private sealed class ThrowingInvoicesService : WebApp.Services.Invoices.IInvoicesService
    {
        public Task<WebApp.ViewModels.Invoices.InvoiceListViewModel> GetInvoiceListAsync(string connectionString, WebApp.Models.Invoices.GetInvoicesQuery query)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<WebApp.ViewModels.Invoices.InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private sealed class PagingInvoicesService : WebApp.Services.Invoices.IInvoicesService
    {
        public int RequestedPageSize { get; private set; }

        public Task<WebApp.ViewModels.Invoices.InvoiceListViewModel> GetInvoiceListAsync(
            string connectionString,
            GetInvoicesQuery query)
        {
            RequestedPageSize = query.PageSize.GetValueOrDefault();
            return Task.FromResult(new WebApp.ViewModels.Invoices.InvoiceListViewModel
            {
                TotalCount = 2,
                UnpaidInvoices = new[]
                {
                    Invoice("C-1", new DateTime(2026, 6, 1)),
                    Invoice("C-2", new DateTime(2026, 6, 3))
                }
            });
        }

        public Task<InvoiceItem?> GetInvoiceAsync(
            string connectionString,
            int? companyCode,
            string invoiceNo)
            => Task.FromResult<InvoiceItem?>(null);

        public Task<WebApp.ViewModels.Invoices.InvoiceListViewModel> GetDashboardSummaryAsync(
            string connectionString,
            int? companyCode)
            => Task.FromResult(new WebApp.ViewModels.Invoices.InvoiceListViewModel());
    }

    private sealed class SuccessfulRuntimeContextService : WebApp.Services.Application.IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                ConnectionString = "Server=.;Database=Jeeves;",
                CompanyCode = 5
            }));
    }

    private sealed class FailingRuntimeContextService : WebApp.Services.Application.IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Fail("authorization=secret-value runtime context failure"));
    }

    private sealed class FakeDemoDataService : IBankReconciliationDemoDataService
    {
        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationDemoScenario());

        public Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoData());

        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => Array.Empty<BankReconciliationDemoScenarioOption>();
    }

    private sealed class FakeSupplierInvoiceService : IBankReconciliationSupplierInvoiceService
    {
        public Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(string connectionString, BankReconciliationSupplierInvoiceQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(((IReadOnlyList<InvoiceItem>)Array.Empty<InvoiceItem>(), 0));
    }

    private sealed class PagingSupplierInvoiceService : IBankReconciliationSupplierInvoiceService
    {
        public int RequestedPageSize { get; private set; }

        public Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(
            string connectionString,
            BankReconciliationSupplierInvoiceQuery query,
            CancellationToken cancellationToken = default)
        {
            RequestedPageSize = query.PageSize.GetValueOrDefault();
            IReadOnlyList<InvoiceItem> invoices = new[]
            {
                Invoice("S-1", new DateTime(2026, 6, 2), isSupplier: true),
                Invoice("S-2", new DateTime(2026, 6, 4), isSupplier: true)
            };
            return Task.FromResult((invoices, 2));
        }
    }

    private static InvoiceItem Invoice(
        string invoiceNo,
        DateTime dueDate,
        bool isSupplier = false)
        => new()
        {
            InvoiceNo = invoiceNo,
            Customer = invoiceNo,
            DueDate = dueDate,
            AmountSek = 100m,
            RemainingAmount = 100m,
            IsSupplierInvoice = isSupplier
        };

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
