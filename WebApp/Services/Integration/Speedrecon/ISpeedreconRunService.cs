using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon;

// Runs Speedrecon from hub-owned orchestration instead of installed Speedrecon procedures.
public interface ISpeedreconRunService
{
    Task<SpeedreconRunOutcome> RunAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default);
}
