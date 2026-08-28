using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Helpers;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Page query tests cover the composed bank reconciliation start page, not controller plumbing.
public sealed class BankReconciliationPageQueryServiceTests
{
    [Fact]
    public async Task BuildPageAsync_UsesDemoStateAndMapsCodingRules()
    {
        var service = new BankReconciliationPageQueryService(
            new DemoRuntimeContextService(),
            new FakeWorkspaceService
            {
                Source = new BankReconciliationSourceContext
                {
                    IsDemoMode = true,
                    HasSource = true,
                    DemoScenarioKey = "partial-payments",
                    BankAccountKey = "SEB-123",
                    BankAccountLabel = "SEB Företagskonto",
                    SourceLabel = "demo.xml",
                    SourceUpdatedAt = new DateTime(2026, 6, 17, 10, 15, 0, DateTimeKind.Utc)
                },
                CodingRules = new BankReconciliationCodingRuleSet
                {
                    Version = 12,
                    Rows =
                    [
                        new BankReconciliationCodingRuleRow { RowId = "row-1", Account = "6570" }
                    ]
                }
            },
            new FakeUploadFlowService("ignored.xml", "customer-statement.nda"),
            new FakeDemoSessionService
            {
                IsDemoModeValue = true,
                ScenarioKey = "partial-payments"
            },
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPageQueryService>.Instance);

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1", CompanyName = "Demo AB" },
            "upload error",
            "upload info",
            "status message",
            "warning",
            CancellationToken.None);

        Assert.Equal("upload error", model.UploadError);
        Assert.Equal("upload info", model.UploadInfo);
        Assert.Equal("status message", model.StatusMessage);
        Assert.Equal("warning", model.StatusTone);
        Assert.True(model.IsDemoMode);
        Assert.Equal("partial-payments", model.DemoScenarioKey);
        Assert.Equal("SEB-123", model.BankAccountKey);
        Assert.Equal("SEB Företagskonto", model.BankAccountLabel);
        Assert.Equal("Demo AB", model.ActiveCompanyName);
        Assert.Equal(12, model.CodingRulesVersion);
        Assert.Contains("row-1", model.CodingRulesJson);
        Assert.NotNull(model.RuntimeBanner);
        Assert.Equal("BankRec_DemoActiveTitle", model.RuntimeBanner!.Title);
        Assert.Equal("info", model.RuntimeBanner.Tone);
        Assert.True(model.HasUploadedFile);
        Assert.Equal("customer-statement.nda", model.LatestFileName);
    }

    [Fact]
    public async Task BuildPageAsync_ShowsTenantWarning_WhenRuntimeContextUnavailable()
    {
        var service = new BankReconciliationPageQueryService(
            new FailingRuntimeContextService(),
            new FakeWorkspaceService
            {
                Source = new BankReconciliationSourceContext
                {
                    HasSource = true,
                    ErrorMessage = "could not read CAMT",
                    SourceLabel = "statement.xml",
                    SourceUpdatedAt = new DateTime(2026, 6, 17, 11, 0, 0, DateTimeKind.Utc)
                }
            },
            new FakeUploadFlowService("statement.xml"),
            new FakeDemoSessionService { IsDemoModeValue = false, ScenarioKey = "overview" },
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new CapturingLogger<BankReconciliationPageQueryService>());

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            null,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.False(model.IsDemoMode);
        Assert.NotNull(model.RuntimeBanner);
        Assert.Equal("warning", model.RuntimeBanner!.Tone);
        Assert.Equal("Tenantdata från Jeeves är tillfälligt otillgänglig", model.RuntimeBanner.Title);
        Assert.Contains("Referens:", model.RuntimeBanner.Note);
        Assert.DoesNotContain("authorization=secret-value", model.RuntimeBanner.Note, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("could not read CAMT", model.UploadError);
        Assert.True(model.HasUploadedFile);
        Assert.Equal("statement.xml", model.LatestFileName);
    }

    [Fact]
    public async Task BuildPageAsync_MapsValidationReportForActiveCamtFile()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var camtFile = Path.Combine(
            webAppRoot,
            "Data",
            "Integration",
            "BankReconciliation",
            "demo",
            "ai-camt-lab.camt053.xml");
        var service = new BankReconciliationPageQueryService(
            new DemoRuntimeContextService(),
            new FakeWorkspaceService
            {
                Source = new BankReconciliationSourceContext
                {
                    HasSource = true,
                    SourceLabel = "ai-camt-lab.camt053.xml"
                }
            },
            new FakeUploadFlowService(camtFile),
            new FakeDemoSessionService(),
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPageQueryService>.Instance);

        var model = await service.BuildPageAsync(
            new UserSession { CompanyId = Guid.NewGuid(), UserId = "user-1" },
            null,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.NotNull(model.ValidationReport);
        Assert.True(model.ValidationReport!.IsValid);
        Assert.Equal(14, model.ValidationReport.TransactionCount);
        Assert.Equal("SE35 •••• 0003", model.ValidationReport.MaskedAccount);
    }

    private sealed class DemoRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext()));
    }

    private sealed class FailingRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Fail("authorization=secret-value missing tenant data"));
    }

    private sealed class FakeWorkspaceService : IBankReconciliationWorkspaceService
    {
        public BankReconciliationSourceContext Source { get; init; } = new();
        public BankReconciliationCodingRuleSet CodingRules { get; init; } = new();

        public Task<BankReconciliationSourceContext> ResolveSourceAsync(
            UserSession? user,
            string? sessionFile,
            bool isDemoMode,
            string demoScenarioKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Source);

        public Task<BankReconciliationCodingRuleSet> LoadCodingRulesAsync(
            UserSession? user,
            BankReconciliationSourceContext source,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CodingRules);

        public Task ResetDemoScenarioAsync(
            Guid companyId,
            string scenarioKey,
            UserSession? user,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUploadFlowService : IBankReconciliationUploadFlowService
    {
        private readonly string? _latestFile;

        private readonly string? _displayName;

        public FakeUploadFlowService(string? latestFile, string? displayName = null)
        {
            _latestFile = latestFile;
            _displayName = displayName;
        }

        public Task<BankReconciliationUploadFlowResult> UploadAsync(
            Microsoft.AspNetCore.Http.IFormFile? file,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationUploadFlowResult());

        public BankReconciliationUploadFlowResult ClearUpload()
            => new();

        public string? ResolveLatestCamtFile()
            => _latestFile;

        public string? ResolveLatestCamtDisplayName()
            => _displayName;
    }

    private sealed class FakeDemoSessionService : IBankReconciliationDemoSessionService
    {
        public bool IsDemoModeValue { get; init; }
        public string ScenarioKey { get; init; } = "overview";

        public bool IsDemoMode(Guid companyId) => IsDemoModeValue;
        public string ResolveScenarioKey(Guid companyId) => ScenarioKey;
        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => [new BankReconciliationDemoScenarioOption { Key = ScenarioKey }];
        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(new BankReconciliationDemoScenario());
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
