using Microsoft.AspNetCore.Http;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineRequestNormalizer
{
    FlowEngineExecuteJobRequest Normalize(FlowEngineExecuteJobRequest request, IFormCollection? form);
}
