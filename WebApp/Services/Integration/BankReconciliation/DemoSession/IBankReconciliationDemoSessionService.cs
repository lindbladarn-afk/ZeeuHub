using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.DemoSession;

// Manages per-company demo mode and scenario selection for bank reconciliation.
public interface IBankReconciliationDemoSessionService
{
    bool IsDemoMode(Guid companyId);
    string ResolveScenarioKey(Guid companyId);
    IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios();

    Task<BankReconciliationDemoScenario> LoadScenarioAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task ToggleDemoModeAsync(
        Guid companyId,
        UserSession? user,
        CancellationToken cancellationToken);

    Task<BankReconciliationDemoSessionResult> SelectScenarioAsync(
        Guid companyId,
        UserSession? user,
        string? scenarioKey,
        CancellationToken cancellationToken);

    Task<BankReconciliationDemoSessionResult> ResetScenarioAsync(
        Guid companyId,
        UserSession? user,
        CancellationToken cancellationToken);
}
