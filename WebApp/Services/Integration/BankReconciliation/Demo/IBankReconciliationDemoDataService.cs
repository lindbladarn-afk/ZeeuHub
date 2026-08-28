using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

public interface IBankReconciliationDemoDataService
{
    Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default);
    Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken = default);
    IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios();
}
