using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Workspace;

public interface IBankReconciliationWorkspaceService
{
    Task<BankReconciliationSourceContext> ResolveSourceAsync(
        UserSession? user,
        string? sessionFile,
        bool isDemoMode,
        string demoScenarioKey,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationCodingRuleSet> LoadCodingRulesAsync(
        UserSession? user,
        BankReconciliationSourceContext source,
        CancellationToken cancellationToken = default);

    Task ResetDemoScenarioAsync(
        Guid companyId,
        string scenarioKey,
        UserSession? user,
        CancellationToken cancellationToken = default);
}
