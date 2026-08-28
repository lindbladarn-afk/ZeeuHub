using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon;

// Executes one ported Speedrecon calculation module for a selected Jeeves tenant.
public interface ISpeedreconModuleRunner
{
    SpeedreconModuleDefinition Definition { get; }

    Task RunAsync(
        JeevesRuntimeContext runtimeContext,
        SpeedreconRunPlan plan,
        CancellationToken cancellationToken = default);
}
