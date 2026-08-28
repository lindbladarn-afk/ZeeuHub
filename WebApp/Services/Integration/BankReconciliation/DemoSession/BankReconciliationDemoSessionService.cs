using Entities.Application;
using Microsoft.Extensions.Localization;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.DemoSession;

// Persists and localizes the bank reconciliation demo workspace selection.
public sealed class BankReconciliationDemoSessionService : IBankReconciliationDemoSessionService
{
    private const string DemoModeSessionKeyPrefix = "BankReconciliation.DemoMode";
    private const string DemoScenarioSessionKeyPrefix = "BankReconciliation.DemoScenario";
    private const string DefaultScenarioKey = "ai-camt-lab";

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IBankReconciliationDemoDataService _demoDataService;
    private readonly IBankReconciliationWorkspaceService _workspaceService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

    public BankReconciliationDemoSessionService(
        IHttpContextAccessor contextAccessor,
        IBankReconciliationDemoDataService demoDataService,
        IBankReconciliationWorkspaceService workspaceService,
        IStringLocalizer<SharedResources> sharedLocalizer)
    {
        _contextAccessor = contextAccessor;
        _demoDataService = demoDataService;
        _workspaceService = workspaceService;
        _sharedLocalizer = sharedLocalizer;
    }

    public bool IsDemoMode(Guid companyId)
        => Session.GetString($"{DemoModeSessionKeyPrefix}.{companyId:N}") == "1";

    public string ResolveScenarioKey(Guid companyId)
        => NormalizeScenarioKey(Session.GetString($"{DemoScenarioSessionKeyPrefix}.{companyId:N}"));

    public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
        => _demoDataService.ListScenarios().Select(LocalizeScenarioOption).ToList();

    public async Task<BankReconciliationDemoScenario> LoadScenarioAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var scenario = await _demoDataService.LoadScenarioAsync(ResolveScenarioKey(companyId), cancellationToken);
        return LocalizeScenario(scenario);
    }

    public async Task ToggleDemoModeAsync(
        Guid companyId,
        UserSession? user,
        CancellationToken cancellationToken)
    {
        var nextMode = !IsDemoMode(companyId);
        PersistDemoMode(companyId, nextMode);
        if (!nextMode)
        {
            return;
        }

        PersistScenarioKey(companyId, DefaultScenarioKey);
        await _workspaceService.ResetDemoScenarioAsync(companyId, DefaultScenarioKey, user, cancellationToken);
    }

    public async Task<BankReconciliationDemoSessionResult> SelectScenarioAsync(
        Guid companyId,
        UserSession? user,
        string? scenarioKey,
        CancellationToken cancellationToken)
    {
        var normalizedScenario = NormalizeScenarioKey(scenarioKey);
        PersistDemoMode(companyId, true);
        PersistScenarioKey(companyId, normalizedScenario);
        await _workspaceService.ResetDemoScenarioAsync(companyId, normalizedScenario, user, cancellationToken);

        var scenario = ListScenarios()
            .FirstOrDefault(x => string.Equals(x.Key, normalizedScenario, StringComparison.OrdinalIgnoreCase));

        return new BankReconciliationDemoSessionResult
        {
            StatusTone = "info",
            StatusMessage = _sharedLocalizer["BankRec_DemoScenarioLoaded", scenario?.Title ?? normalizedScenario].Value
        };
    }

    public async Task<BankReconciliationDemoSessionResult> ResetScenarioAsync(
        Guid companyId,
        UserSession? user,
        CancellationToken cancellationToken)
    {
        var scenarioKey = ResolveScenarioKey(companyId);
        PersistDemoMode(companyId, true);
        await _workspaceService.ResetDemoScenarioAsync(companyId, scenarioKey, user, cancellationToken);

        return new BankReconciliationDemoSessionResult
        {
            StatusTone = "info",
            StatusMessage = _sharedLocalizer["BankRec_DemoScenarioReset"].Value
        };
    }

    private ISession Session => _contextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Bank reconciliation demo session requires an active HTTP session.");

    private void PersistDemoMode(Guid companyId, bool enabled)
        => Session.SetString($"{DemoModeSessionKeyPrefix}.{companyId:N}", enabled ? "1" : "0");

    private void PersistScenarioKey(Guid companyId, string scenarioKey)
        => Session.SetString($"{DemoScenarioSessionKeyPrefix}.{companyId:N}", NormalizeScenarioKey(scenarioKey));

    private static string NormalizeScenarioKey(string? scenarioKey)
        => string.IsNullOrWhiteSpace(scenarioKey) ? DefaultScenarioKey : scenarioKey.Trim().ToLowerInvariant();

    private BankReconciliationDemoScenario LocalizeScenario(BankReconciliationDemoScenario scenario)
    {
        var option = LocalizeScenarioOption(new BankReconciliationDemoScenarioOption { Key = scenario.Key });
        scenario.Title = option.Title;
        scenario.Description = option.Description;
        return scenario;
    }

    private BankReconciliationDemoScenarioOption LocalizeScenarioOption(BankReconciliationDemoScenarioOption option)
    {
        var normalizedKey = NormalizeScenarioKey(option.Key);
        return normalizedKey switch
        {
            "manual-review" => new BankReconciliationDemoScenarioOption
            {
                Key = normalizedKey,
                Title = _sharedLocalizer["BankRec_DemoScenario_ManualReview_Title"].Value,
                Description = _sharedLocalizer["BankRec_DemoScenario_ManualReview_Description"].Value
            },
            "partial-payments" => new BankReconciliationDemoScenarioOption
            {
                Key = normalizedKey,
                Title = _sharedLocalizer["BankRec_DemoScenario_PartialPayments_Title"].Value,
                Description = _sharedLocalizer["BankRec_DemoScenario_PartialPayments_Description"].Value
            },
            "ai-camt-lab" => new BankReconciliationDemoScenarioOption
            {
                Key = normalizedKey,
                Title = _sharedLocalizer["BankRec_DemoScenario_AiCamtLab_Title"].Value,
                Description = _sharedLocalizer["BankRec_DemoScenario_AiCamtLab_Description"].Value
            },
            _ => new BankReconciliationDemoScenarioOption
            {
                Key = normalizedKey,
                Title = _sharedLocalizer["BankRec_DemoScenario_Overview_Title"].Value,
                Description = _sharedLocalizer["BankRec_DemoScenario_Overview_Description"].Value
            }
        };
    }
}
