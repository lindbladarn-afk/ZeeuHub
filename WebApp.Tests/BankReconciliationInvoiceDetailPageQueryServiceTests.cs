using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Helpers;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Invoice detail tests cover the invoice-specific bank reconciliation page composition.
public sealed class BankReconciliationInvoiceDetailPageQueryServiceTests
{
    [Fact]
    public async Task BuildPageAsync_UsesDemoInvoicesAndRuntimeBanner()
    {
        var service = new BankReconciliationInvoiceDetailPageQueryService(
            new DemoRuntimeContextService(),
            new FakeInvoiceCandidateService(),
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoiceDetailPageQueryService>.Instance);

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            new BankReconciliationSourceContext
            {
                IsDemoMode = true,
                HasSource = true,
                SourceLabel = "demo.xml",
                SourceUpdatedAt = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc),
                Transactions =
                [
                    new BankReconciliationParsedTransaction { Id = "tx-1", Amount = 100m, Currency = "SEK" }
                ]
            },
            CancellationToken.None);

        Assert.True(model.IsDemoMode);
        Assert.True(model.HasUploadedFile);
        Assert.Equal("demo.xml", model.LatestFileName);
        Assert.Contains("tx-1", model.TransactionsJson);
        Assert.Contains("demo-invoice", model.InvoicesJson);
        Assert.NotNull(model.RuntimeBanner);
        Assert.Equal("BankRec_DemoActiveTitle", model.RuntimeBanner!.Title);
    }

    [Fact]
    public async Task BuildPageAsync_ShowsWarning_WhenRuntimeContextMissing()
    {
        var logger = new CapturingLogger<BankReconciliationInvoiceDetailPageQueryService>();
        var service = new BankReconciliationInvoiceDetailPageQueryService(
            new FailingRuntimeContextService(),
            new FakeInvoiceCandidateService(),
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger);

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            new BankReconciliationSourceContext
            {
                IsDemoMode = false,
                HasSource = true,
                SourceLabel = "statement.xml"
            },
            CancellationToken.None);

        Assert.False(model.IsDemoMode);
        Assert.NotNull(model.RuntimeBanner);
        Assert.Equal("warning", model.RuntimeBanner!.Tone);
        Assert.Equal("Tenantdata från Jeeves är tillfälligt otillgänglig", model.RuntimeBanner.Title);
        Assert.Contains("Referens:", model.RuntimeBanner.Note);
        Assert.DoesNotContain("missing tenant data", model.RuntimeBanner.Note, StringComparison.OrdinalIgnoreCase);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task BuildPageAsync_ShowsSafeNote_WhenInvoiceLoadFails()
    {
        var logger = new CapturingLogger<BankReconciliationInvoiceDetailPageQueryService>();
        var service = new BankReconciliationInvoiceDetailPageQueryService(
            new DemoRuntimeContextService(),
            new ThrowingInvoiceCandidateService(),
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger);

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            new BankReconciliationSourceContext
            {
                IsDemoMode = false,
                HasSource = true,
                SourceLabel = "statement.xml"
            },
            CancellationToken.None);

        Assert.NotNull(model.RuntimeBanner);
        Assert.Contains("Referens:", model.RuntimeBanner!.Note);
        Assert.DoesNotContain("authorization=secret-value", model.RuntimeBanner.Note, StringComparison.OrdinalIgnoreCase);
        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("BankReconciliationInvoiceDetailPageQueryService failed", errorLog.Message);
        Assert.Contains("SupportId=", errorLog.Message);
    }

    private sealed class DemoRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext()));
    }

    private sealed class FailingRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Fail("missing tenant data"));
    }

    private sealed class FakeInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
    {
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
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult
            {
                Invoices =
                [
                    new InvoiceItem
                    {
                        InvoiceNo = "demo-invoice",
                        Customer = "Demo Customer",
                        AmountSek = 123m,
                        DueDate = new DateTime(2026, 6, 18)
                    }
                ]
            });

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(UserSession user, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(UserSession user, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());
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
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class FakeDemoSessionService : IBankReconciliationDemoSessionService
    {
        public bool IsDemoMode(Guid companyId) => true;
        public string ResolveScenarioKey(Guid companyId) => "overview";
        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios() => [];

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationDemoScenario
            {
                Title = "Overview",
                Description = "Demo data",
                Data = new BankReconciliationDemoData
                {
                    Invoices =
                    [
                        new BankReconciliationDemoInvoice
                        {
                            Id = "demo-invoice",
                            InvoiceNo = "INV-100",
                            CustomerName = "Demo Customer",
                            Amount = 123m,
                            Currency = "SEK",
                            DueDate = "2026-06-18"
                        }
                    ]
                }
            });

        public Task ToggleDemoModeAsync(Guid companyId, UserSession? user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BankReconciliationDemoSessionResult> SelectScenarioAsync(Guid companyId, UserSession? user, string? scenarioKey, CancellationToken cancellationToken) => Task.FromResult(new BankReconciliationDemoSessionResult());
        public Task<BankReconciliationDemoSessionResult> ResetScenarioAsync(Guid companyId, UserSession? user, CancellationToken cancellationToken) => Task.FromResult(new BankReconciliationDemoSessionResult());
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
