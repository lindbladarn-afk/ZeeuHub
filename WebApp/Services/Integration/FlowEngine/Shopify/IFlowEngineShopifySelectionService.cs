using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifySelectionService
{
    FlowEngineShopifyDateSelection ResolveDateSelection(FlowEngineExecuteJobRequest request);
    string FormatDateUtc(DateTime date);
    List<DateTime> EnumerateDates(DateTime sinceUtc, DateTime untilUtc);
    bool RequiresReadAllOrders(DateTime earliestDateUtc);
    string BuildDateSearchQuery(DateTime dateUtc);
    string? ParseUpdatedSince(string? rawValue);
    string? BuildProductsSearchQuery(string? baseQuery, string? updatedSince);
    string BuildSelectionSummaryLabel(string selectionKind, string? date, string? sinceUtc, string? untilUtc);
    string NormalizeOrderGid(string? rawOrderId);
    string? ExtractNumericIdFromGid(string? orderGid);
}
