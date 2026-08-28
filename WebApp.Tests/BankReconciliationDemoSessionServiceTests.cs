using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Localization;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Demo session tests cover per-company demo state outside the MVC controller.
public sealed class BankReconciliationDemoSessionServiceTests
{
    [Fact]
    public async Task SelectScenario_NormalizesKeyEnablesDemoAndResetsWorkspace()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        var workspace = new FakeWorkspaceService();
        var service = new BankReconciliationDemoSessionService(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeDemoDataService(),
            workspace,
            new DummyStringLocalizer());
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var user = new UserSession { UserId = "user-1", CompanyId = companyId };

        var result = await service.SelectScenarioAsync(companyId, user, " Partial-Payments ", CancellationToken.None);

        Assert.True(service.IsDemoMode(companyId));
        Assert.Equal("partial-payments", service.ResolveScenarioKey(companyId));
        Assert.Equal("partial-payments", workspace.LastScenarioKey);
        Assert.Equal("BankRec_DemoScenarioLoaded", result.StatusMessage);
    }

    private sealed class FakeWorkspaceService : IBankReconciliationWorkspaceService
    {
        public string? LastScenarioKey { get; private set; }

        public Task<BankReconciliationSourceContext> ResolveSourceAsync(
            UserSession? user,
            string? sessionFile,
            bool isDemoMode,
            string demoScenarioKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationSourceContext());

        public Task<BankReconciliationCodingRuleSet> LoadCodingRulesAsync(
            UserSession? user,
            BankReconciliationSourceContext source,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());

        public Task ResetDemoScenarioAsync(
            Guid companyId,
            string scenarioKey,
            UserSession? user,
            CancellationToken cancellationToken = default)
        {
            LastScenarioKey = scenarioKey;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDemoDataService : IBankReconciliationDemoDataService
    {
        public Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoData());

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(
            string? scenarioKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoScenario { Key = scenarioKey ?? "ai-camt-lab" });

        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => new[]
            {
                new BankReconciliationDemoScenarioOption { Key = "partial-payments" }
            };
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
