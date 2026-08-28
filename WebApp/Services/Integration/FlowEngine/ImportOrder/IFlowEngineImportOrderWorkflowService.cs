using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineImportOrderWorkflowService
{
    FlowEngineImportOrderSessionState? LoadState();
    void SaveState(FlowEngineImportOrderSessionState state);
    FlowEngineRunImportOrderInput NormalizeInput(FlowEngineRunImportOrderInput input);
    FlowEngineImportOrderSessionState BuildState(
        FlowEngineRunImportOrderInput form,
        FlowEngineImportOrderSessionState? currentState,
        IReadOnlyCollection<FlowEngineDeliveryAddressOption>? deliveryAddressOptions = null,
        FlowEngineImportAddressLookupContext? addressLookupContext = null,
        FlowEngineImportDocumentReview? documentReview = null,
        IReadOnlyCollection<FlowEngineJeevesArtStatusRow>? artStatusRows = null);
    string? ResolveDeliveryPlaceCode(int companyCode, string customerNumber, string? selectedCode);
    List<FlowEngineDeliveryAddressOption> ParseDeliveryAddressOptionsFromJob(FlowEngineJobSnapshot job);
    List<FlowEngineJeevesArtStatusRow> ParseArtStatusRowsFromJob(FlowEngineJobSnapshot job);
    FlowEngineImportDocumentReview BuildDocumentReview(string fileName, FlowEngineOrderDocumentExtractionResult extractionResult);
    FlowEngineImportDocumentReview BuildDocumentErrorReview(string? fileName, string errorMessage);
    string MergeDocumentLines(string currentLines, IReadOnlyCollection<FlowEngineImportDocumentReviewLine> extractedLines);
    List<FlowEngineJeevesImportLineInput> ParseImportOrderLines(string rawLines);
}
