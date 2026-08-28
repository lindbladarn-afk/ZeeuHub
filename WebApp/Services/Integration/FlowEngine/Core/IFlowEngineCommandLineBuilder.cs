using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCommandLineBuilder
{
    IReadOnlyList<string> BuildArguments(FlowEngineExecuteJobRequest request, JeevesRuntimeContext runtimeContext);
}
