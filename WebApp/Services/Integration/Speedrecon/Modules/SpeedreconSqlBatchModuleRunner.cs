using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon.Modules;

// Base class for Speedrecon modules that now live as explicit hub-owned SQL batches.
public abstract class SpeedreconSqlBatchModuleRunner : ISpeedreconModuleRunner
{
    private readonly ISpeedreconRepository _repository;

    protected SpeedreconSqlBatchModuleRunner(
        SpeedreconModuleDefinition definition,
        ISpeedreconRepository repository)
    {
        Definition = definition;
        _repository = repository;
    }

    public SpeedreconModuleDefinition Definition { get; }

    protected abstract string Sql { get; }

    public Task RunAsync(
        JeevesRuntimeContext runtimeContext,
        SpeedreconRunPlan plan,
        CancellationToken cancellationToken = default)
        => _repository.ExecuteBatchAsync(runtimeContext, Sql, plan.ReconDate, cancellationToken);
}
