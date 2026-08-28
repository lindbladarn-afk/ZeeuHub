using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineHealthProbeService
{
    Task<IReadOnlyList<FlowEngineSystemStatusViewModel>> ProbeAsync(
        UserSession? sessionUser,
        string activeSection,
        JeevesRuntimeContext? runtimeContext,
        bool testMode,
        CancellationToken cancellationToken = default);
}
