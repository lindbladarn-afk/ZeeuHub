using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Invoice page query tests cover invoice paging decisions without MVC.
public sealed class BankReconciliationInvoicePageQueryServiceTests
{
    [Fact]
    public async Task BuildPage_DemoSupplierClassificationDoesNotLoadCustomerInvoices()
    {
        var invoiceService = new FakeInvoiceCandidateService();
        var service = new BankReconciliationInvoicePageQueryService(
            invoiceService,
            new FakeDemoSessionService { DemoMode = true },
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoicePageQueryService>.Instance);

        var result = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            page: 1,
            pageSize: 20,
            classificationFilter: "leverantorsbetalning",
            groupFilter: "all",
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.False(invoiceService.LoadAsyncCalled);
    }

    [Fact]
    public async Task BuildPage_LiveCustomerClassificationUsesCustomerPage()
    {
        var invoiceService = new FakeInvoiceCandidateService
        {
            CustomerResult = new BankReconciliationInvoiceCandidateResult
            {
                TotalCount = 21,
                Invoices =
                {
                    new InvoiceItem
                    {
                        InvoiceNo = "INV-1",
                        Customer = "Kund AB",
                        AmountSek = 100m,
                        DueDate = new DateTime(2026, 6, 1)
                    }
                }
            }
        };
        var service = new BankReconciliationInvoicePageQueryService(
            invoiceService,
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoicePageQueryService>.Instance);

        var result = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            page: 2,
            pageSize: 20,
            classificationFilter: "bankinbetalningar",
            groupFilter: "all",
            CancellationToken.None);

        Assert.True(invoiceService.LoadCustomerPageCalled);
        Assert.Single(result.Items);
        Assert.Equal("INV-1", result.Items[0].InvoiceNo);
        Assert.Equal(21, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task BuildPage_LiveAllTypesUsesCombinedDatabasePage()
    {
        var invoiceService = new FakeInvoiceCandidateService
        {
            CombinedResult = new BankReconciliationInvoiceCandidateResult
            {
                TotalCount = 42,
                Invoices =
                {
                    new InvoiceItem
                    {
                        InvoiceNo = "SUP-1",
                        Customer = "Leverantör AB",
                        IsSupplierInvoice = true,
                        AmountSek = 250m,
                        DueDate = new DateTime(2026, 6, 2)
                    }
                }
            }
        };
        var service = new BankReconciliationInvoicePageQueryService(
            invoiceService,
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoicePageQueryService>.Instance);

        var result = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid() },
            page: 2,
            pageSize: 20,
            classificationFilter: "all",
            groupFilter: "all",
            CancellationToken.None);

        Assert.True(invoiceService.LoadCombinedPageCalled);
        Assert.False(invoiceService.LoadAsyncCalled);
        Assert.Equal(42, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task BuildPage_LiveFailure_ReturnsSafeErrorMessage()
    {
        var invoiceService = new ThrowingInvoiceCandidateService();
        var logger = new CapturingLogger<BankReconciliationInvoicePageQueryService>();
        var service = new BankReconciliationInvoicePageQueryService(
            invoiceService,
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger);

        var result = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            page: 1,
            pageSize: 20,
            classificationFilter: "bankinbetalningar",
            groupFilter: "all",
            CancellationToken.None);

        Assert.Contains("Referens:", result.ErrorMessage);
        Assert.DoesNotContain("authorization=secret-value", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("BankReconciliationInvoicePageQueryService failed", errorLog.Message);
        Assert.Contains("SupportId=", errorLog.Message);
    }

    private sealed class FakeInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
    {
        public bool LoadAsyncCalled { get; private set; }
        public bool LoadCustomerPageCalled { get; private set; }
        public bool LoadCombinedPageCalled { get; private set; }
        public BankReconciliationInvoiceCandidateResult CustomerResult { get; set; } = new();
        public BankReconciliationInvoiceCandidateResult CombinedResult { get; set; } = new();

        public Task<BankReconciliationInvoiceCandidateResult> LoadAsync(
            bool isDemoMode,
            UserSession user,
            CancellationToken cancellationToken,
            BankReconciliationParsedTransaction? transaction = null,
            string? classificationFilter = null,
            string? groupFilter = null,
            int? page = null,
            int? pageSize = null,
            string? demoScenarioKey = null)
        {
            LoadAsyncCalled = true;
            return Task.FromResult(new BankReconciliationInvoiceCandidateResult());
        }

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LoadCustomerPageCalled = true;
            return Task.FromResult(CustomerResult);
        }

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());

        public Task<BankReconciliationInvoiceCandidateResult> LoadCombinedPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LoadCombinedPageCalled = true;
            return Task.FromResult(CombinedResult);
        }
    }

    private sealed class FakeDemoSessionService : IBankReconciliationDemoSessionService
    {
        public bool DemoMode { get; set; }
        public bool IsDemoMode(Guid companyId) => DemoMode;
        public string ResolveScenarioKey(Guid companyId) => "ai-camt-lab";
        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios() => Array.Empty<BankReconciliationDemoScenarioOption>();

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(
            Guid companyId,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationDemoScenario());

        public Task ToggleDemoModeAsync(
            Guid companyId,
            UserSession? user,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<BankReconciliationDemoSessionResult> SelectScenarioAsync(
            Guid companyId,
            UserSession? user,
            string? scenarioKey,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationDemoSessionResult());

        public Task<BankReconciliationDemoSessionResult> ResetScenarioAsync(
            Guid companyId,
            UserSession? user,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationDemoSessionResult());
    }

    private sealed class ThrowingInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
    {
        public Task<BankReconciliationInvoiceCandidateResult> LoadAsync(bool isDemoMode, UserSession user, CancellationToken cancellationToken, BankReconciliationParsedTransaction? transaction = null, string? classificationFilter = null, string? groupFilter = null, int? page = null, int? pageSize = null, string? demoScenarioKey = null)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(UserSession user, int page, int pageSize, CancellationToken cancellationToken)
            => throw new InvalidOperationException("authorization=secret-value");

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(UserSession user, int page, int pageSize, CancellationToken cancellationToken)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}
