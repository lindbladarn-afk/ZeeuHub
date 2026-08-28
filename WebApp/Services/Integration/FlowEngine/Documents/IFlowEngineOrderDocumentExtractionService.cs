using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineOrderDocumentExtractionService
{
    Task<FlowEngineOrderDocumentExtractionResult> ExtractAsync(
        FlowEngineOrderDocumentInput document,
        CancellationToken cancellationToken = default);
}
