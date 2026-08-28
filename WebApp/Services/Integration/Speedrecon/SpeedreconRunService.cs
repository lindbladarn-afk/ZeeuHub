using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon;

// Orchestrates Speedrecon in C# using the same module order as the original SQL start procedure.
public sealed class SpeedreconRunService : ISpeedreconRunService
{
    private readonly ISpeedreconRepository _repository;
    private readonly IReadOnlyList<ISpeedreconModuleRunner> _moduleRunners;

    public SpeedreconRunService(
        ISpeedreconRepository repository,
        IEnumerable<ISpeedreconModuleRunner> moduleRunners)
    {
        _repository = repository;
        _moduleRunners = moduleRunners.ToList();
    }

    public async Task<SpeedreconRunOutcome> RunAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        var plans = await _repository.GetRunPlansAsync(runtimeContext, reconDate.Date, cancellationToken);
        if (plans.Count == 0)
            throw new InvalidOperationException("Det finns ingen Speedrecon-plan for valt bolag, PersSign och datum.");

        var executedModules = new List<string>();
        foreach (var plan in plans)
        {
            foreach (var runner in _moduleRunners.Where(item => item.Definition.IsEnabled(plan)))
            {
                await _repository.DeleteResultRowsAsync(
                    runtimeContext,
                    plan.ReconDate,
                    runner.Definition.ResultDescriptions,
                    cancellationToken);

                await runner.RunAsync(runtimeContext, plan, cancellationToken);
                executedModules.Add(runner.Definition.DisplayName);
            }

            await _repository.UpdateGeneralLedgerAmountsAsync(runtimeContext, plan.ReconDate, cancellationToken);
        }

        return new SpeedreconRunOutcome
        {
            PlanCount = plans.Count,
            ModuleCount = executedModules.Count,
            ReconDate = reconDate.Date,
            ModuleNames = executedModules
        };
    }
}
