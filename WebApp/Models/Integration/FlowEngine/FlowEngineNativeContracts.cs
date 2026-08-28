using System.Text.Json;

namespace WebApp.Models.Integration;

// Native C# contracts that mirror the typed FlowEngine command surface from the original Swift service.
// These become the stable seam between Razor/UI, job orchestration and concrete integration adapters.

public enum FlowEngineOperationType
{
    CheckOrders = 0,
    CreateShipments = 1,
    SendOrder = 2,
    SendOrders = 3,
    CreateShipment = 4,
    SendReturn = 5,
    SendReturns = 6,
    OrderExists = 7,
    GetCustomerAddresses = 8,
    GetProduct = 9,
    GetArtStatus = 10,
    ImportOrder = 11,
    AkeneoProducts = 12,
    AkeneoAllProducts = 13,
    CompleteOrdersPending = 14,
    CreateShipmentsPending = 15,
    Raw = 16,
    CompleteOrders = 17,
    CompleteOrder = 18,
    AkeneoSendToShopify = 19,
    ShopifyScopesCheck = 20,
    ShopifyGetProducts = 21,
    ShopifyFetchOrder = 22,
    ShopifyFetchOrders = 23,
    ShopifyValidateOrder = 24,
    ShopifyValidateOrders = 25,
    ShopifyCheckOrders = 26,
    ShopifySendOrder = 27,
    ShopifySendOrders = 28,
    GetOrders = 29,
    CentraFetchOrder = 30,
    CentraFetchOrders = 31,
    CentraFetchReturn = 32,
    CentraFetchReturns = 33,
    ConfigValidate = 34,
    AkeneoSendToCentra = 35
}

public enum FlowEngineJobStatus
{
    Queued = 0,
    Running = 1,
    Cancelled = 2,
    Succeeded = 3,
    Failed = 4
}

public enum FlowEngineJobStorageKind
{
    Persistent = 0,
    InMemoryFallback = 1
}

public sealed class FlowEngineExecutionFlags
{
    public bool TestMode { get; set; }
    public bool DryRun { get; set; }
    public bool DebugHttp { get; set; }
    public bool SkipJeevesCheck { get; set; }
    public bool CloseOrder { get; set; }
    public bool ForceRange { get; set; }
}

public sealed class FlowEngineJeevesImportLineInput
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string? Price { get; set; }
}

public sealed class FlowEngineJeevesImportOrderInput
{
    public int? CompanyCode { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public int? OrderType { get; set; }
    public string? CustomerReference { get; set; }
    public string? ExternalOrderNumber { get; set; }
    public string? DeliveryPlaceCode { get; set; }
    public List<FlowEngineJeevesImportLineInput> Lines { get; set; } = new();
}

public sealed class FlowEngineExecutionParams
{
    public string? DateUtc { get; set; }
    public string? SinceUtc { get; set; }
    public string? UntilUtc { get; set; }
    public bool UseLatestDay { get; set; }
    public bool UseLimit { get; set; }
    public int? Limit { get; set; }
    public string? OrderId { get; set; }
    public string? ReturnId { get; set; }
    public string? ShopifyQuery { get; set; }
    public string? ShopifyUpdatedSince { get; set; }
    public int? JeevesCompanyCode { get; set; }
    public string? JeevesCustomerNumber { get; set; }
    public string? JeevesLookupField { get; set; }
    public string? JeevesLookupValue { get; set; }
    public int? JeevesProductCompanyCode { get; set; }
    public string? JeevesProductArticleNumber { get; set; }
    public List<string> JeevesProductArticleNumbers { get; set; } = new();
    public FlowEngineJeevesImportOrderInput? JeevesImportOrder { get; set; }
    public List<string> AkeneoSkus { get; set; } = new();
    public List<string> RawArguments { get; set; } = new();
}

public sealed class FlowEngineExecuteJobRequest
{
    public string? Name { get; set; }
    public string? UiLabel { get; set; }
    public FlowEngineOperationType Operation { get; set; }
    public FlowEngineExecutionFlags Flags { get; set; } = new();
    public FlowEngineExecutionParams Params { get; set; } = new();
}

public sealed class FlowEngineJobResultPayload
{
    public string CommandLine { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool Succeeded { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }
}

public sealed class FlowEngineJobSnapshot
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? UiLabel { get; set; }
    public bool IsScheduled { get; set; }
    public FlowEngineJobStatus Status { get; set; }
    public List<string> Arguments { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public FlowEngineJobResultPayload? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public FlowEngineJobStorageKind StorageKind { get; set; }
    public string? StorageWarning { get; set; }
}

public sealed class FlowEngineOperationExecutionData
{
    public List<string> SummaryLines { get; set; } = new();
    public string JsonOutput { get; set; } = "[]";
}

public sealed class FlowEngineCheckOrdersPayload
{
    public string Date { get; set; } = string.Empty;
    public List<FlowEngineCheckOrdersMissingRow> Missing { get; set; } = new();
    public List<FlowEngineCheckOrdersDeletedRow> Deleted { get; set; } = new();
    public List<FlowEngineCheckOrdersErrorRow> Errors { get; set; } = new();
    public FlowEngineCheckOrdersCounts Counts { get; set; } = new();
    public double RuntimeSeconds { get; set; }
}

public sealed class FlowEngineCheckOrdersCounts
{
    public int Centra { get; set; }
    public int Found { get; set; }
    public int Missing { get; set; }
    public int Deleted { get; set; }
    public int Error { get; set; }
    public int Total { get; set; }
}

public class FlowEngineCheckOrdersMissingRow
{
    public string Id { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public sealed class FlowEngineCheckOrdersDeletedRow : FlowEngineCheckOrdersMissingRow
{
    public string Status { get; set; } = string.Empty;
}

public sealed class FlowEngineCheckOrdersErrorRow : FlowEngineCheckOrdersMissingRow
{
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class FlowEngineSendOrdersPayload
{
    public string Date { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public FlowEngineSendOrdersCounts Counts { get; set; } = new();
    public double RuntimeSeconds { get; set; }
    public List<FlowEngineSendOrdersRow> Orders { get; set; } = new();
}

public sealed class FlowEngineSendOrdersCounts
{
    public int CentraTotal { get; set; }
    public int Mapped { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedIneligible { get; set; }
    public int ManualReviewRequired { get; set; }
    public int Failed { get; set; }
}

public sealed class FlowEngineSendOrdersRow
{
    public string Id { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? PayloadJson { get; set; }
    public List<FlowEngineSendOrdersRuleFailure> ValidationFailures { get; set; } = new();
    public List<FlowEngineSendOrdersRuleFailure> EligibilityFailures { get; set; } = new();
}

public sealed class FlowEngineSendOrderSinglePayload
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public bool SkipJeevesCheck { get; set; }
    public FlowEngineSendOrdersRow Result { get; set; } = new();
    public JsonElement? Payload { get; set; }
}

public sealed class FlowEngineSendOrdersRuleFailure
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FlowEngineAkeneoExportPayload
{
    public string Scope { get; set; } = "all";
    public int Count { get; set; }
    public string FileName { get; set; } = string.Empty;
    public IReadOnlyList<string> RequestedSkus { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> NotFoundSkus { get; set; } = Array.Empty<string>();
    public string Xml { get; set; } = string.Empty;
}

public sealed class FlowEngineAkeneoSendToShopifyPayload
{
    public bool DryRun { get; set; }
    public string Scope { get; set; } = "all";
    public IReadOnlyList<string> RequestedSkus { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> GrantedScopes { get; set; } = Array.Empty<string>();
    public FlowEngineAkeneoSendToShopifyCounts Counts { get; set; } = new();
    public List<FlowEngineAkeneoSendToShopifyItem> Items { get; set; } = new();
}

public sealed class FlowEngineAkeneoSendToCentraPayload
{
    public bool DryRun { get; set; }
    public string Scope { get; set; } = "all";
    public string FileName { get; set; } = string.Empty;
    public int Count { get; set; }
    public IReadOnlyList<string> RequestedSkus { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> NotFoundSkus { get; set; } = Array.Empty<string>();
    public string Xml { get; set; } = string.Empty;
}

public sealed class FlowEngineAkeneoSendToShopifyCounts
{
    public int Requested { get; set; }
    public int Eligible { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int WouldCreate { get; set; }
    public int WouldUpdate { get; set; }
    public int NoChange { get; set; }
    public int Total { get; set; }
}

public sealed class FlowEngineAkeneoSendToShopifyItem
{
    public string Sku { get; set; } = string.Empty;
    public string DecisionStatus { get; set; } = string.Empty;
    public string? DecisionCode { get; set; }
    public string? DecisionMessage { get; set; }
    public bool WouldCreate { get; set; }
    public bool WouldUpdate { get; set; }
    public bool NoChange { get; set; }
    public string? RuntimeError { get; set; }
    public List<FlowEngineAkeneoSendToShopifyWarning> Warnings { get; set; } = new();
    public FlowEngineAkeneoShopifyDraft? Desired { get; set; }
    public FlowEngineAkeneoShopifyDraft? Current { get; set; }
    public FlowEngineAkeneoSendToShopifyUpdatePlan UpdatePlan { get; set; } = new();
}

public sealed class FlowEngineAkeneoSendToShopifyWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FlowEngineAkeneoSendToShopifyUpdatePlan
{
    public bool WouldCreate { get; set; }
    public bool NoChange { get; set; }
    public FlowEngineAkeneoShopifyUpdateDraft WouldUpdate { get; set; } = new();
    public List<FlowEngineAkeneoSendToShopifyDifference> IgnoredDifferences { get; set; } = new();
    public List<FlowEngineAkeneoSendToShopifyWarning> Warnings { get; set; } = new();
}

public sealed class FlowEngineAkeneoSendToShopifyDifference
{
    public string Field { get; set; } = string.Empty;
    public string? Desired { get; set; }
    public string? Current { get; set; }
}

public sealed class FlowEngineAkeneoShopifyUpdateDraft
{
    public string? VariantBarcode { get; set; }
    public List<string> TagsToAdd { get; set; } = new();
    public List<FlowEngineAkeneoShopifyMetafieldDraft> MetafieldsToUpsert { get; set; } = new();
}

public sealed class FlowEngineAkeneoShopifyDraft
{
    public string Sku { get; set; } = string.Empty;
    public FlowEngineAkeneoShopifyProductDraft Product { get; set; } = new();
    public FlowEngineAkeneoShopifyVariantDraft Variant { get; set; } = new();
    public List<FlowEngineAkeneoShopifyMetafieldDraft> Metafields { get; set; } = new();
}

public sealed class FlowEngineAkeneoShopifyProductDraft
{
    public string? Title { get; set; }
    public string? Handle { get; set; }
    public string? Vendor { get; set; }
    public string? ProductType { get; set; }
    public string? Status { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? DescriptionHtml { get; set; }
    public List<string> ImageTokens { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
}

public sealed class FlowEngineAkeneoShopifyVariantDraft
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Price { get; set; }
    public string? CompareAtPrice { get; set; }
}

public sealed class FlowEngineAkeneoShopifyMetafieldDraft
{
    public string Namespace { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class FlowEngineSendReturnsPayload
{
    public string Date { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public FlowEngineSendReturnsCounts Counts { get; set; } = new();
    public double RuntimeSeconds { get; set; }
    public List<FlowEngineSendReturnsRow> Returns { get; set; } = new();
}

public sealed class FlowEngineSendReturnsCounts
{
    public int CentraTotal { get; set; }
    public int Mapped { get; set; }
    public int SkippedIneligible { get; set; }
    public int FailedValidation { get; set; }
    public int FailedMapping { get; set; }
    public int FailedApi { get; set; }
    public int AlreadyExists { get; set; }
}

public sealed class FlowEngineSendReturnsRow
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<FlowEngineSendReturnsRuleFailure> ValidationFailures { get; set; } = new();
    public List<FlowEngineSendReturnsRuleFailure> EligibilityFailures { get; set; } = new();
}

public sealed class FlowEngineSendReturnsRuleFailure
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FlowEngineCreateShipmentsPayload
{
    public string Date { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public FlowEngineCreateShipmentsCounts Counts { get; set; } = new();
    public List<FlowEngineCreateShipmentResultRow> Results { get; set; } = new();
}

public sealed class FlowEngineCreateShipmentPayload
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public FlowEngineCreateShipmentResultRow Result { get; set; } = new();
}

public sealed class FlowEngineShopifyCompleteOrdersPendingPayload
{
    public string Date { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public bool CloseOrder { get; set; }
    public FlowEngineShopifyCompleteOrdersPendingCounts Counts { get; set; } = new();
    public List<FlowEngineShopifyCompleteOrderRow> Orders { get; set; } = new();
}

public sealed class FlowEngineShopifyCompleteOrdersPayload
{
    public string? Date { get; set; }
    public string? SinceUtc { get; set; }
    public string? UntilUtc { get; set; }
    public bool UseLatestDay { get; set; }
    public string SelectionKind { get; set; } = "date";
    public bool DryRun { get; set; }
    public bool CloseOrder { get; set; }
    public FlowEngineShopifyCompleteOrdersPendingCounts Counts { get; set; } = new();
    public List<FlowEngineShopifyCompleteOrderRow> Orders { get; set; } = new();
    public List<FlowEngineShopifyCompleteOrdersDayPayload> Days { get; set; } = new();
}

public sealed class FlowEngineShopifyCompleteOrderPayload
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderGid { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public bool CloseOrder { get; set; }
    public FlowEngineShopifyCompleteOrderRow Result { get; set; } = new();
}

public sealed class FlowEngineShopifyCompleteOrdersDayPayload
{
    public string Date { get; set; } = string.Empty;
    public FlowEngineShopifyCompleteOrdersPendingCounts Counts { get; set; } = new();
    public List<FlowEngineShopifyCompleteOrderRow> Orders { get; set; } = new();
}

public sealed class FlowEngineShopifyCompleteOrdersPendingCounts
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Ready { get; set; }
    public int SkippedAlreadyComplete { get; set; }
    public int SkippedIneligible { get; set; }
    public int Failed { get; set; }
}

public sealed class FlowEngineShopifyCompleteOrderRow
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderGid { get; set; }
    public string Status { get; set; } = string.Empty;
    public FlowEngineShopifyValidationDecision? Validation { get; set; }
    public List<string> FulfillmentOrderIds { get; set; } = new();
    public string? FulfillmentId { get; set; }
    public bool CloseApplied { get; set; }
    public int? JeevesOrderStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FlowEngineShopifyValidationDecision
{
    public string Status { get; set; } = string.Empty;
    public string? RuleId { get; set; }
    public string? Classification { get; set; }
    public string? Message { get; set; }
    public string? Remediation { get; set; }
}

public sealed class FlowEngineShopifyValidateOrderPayload
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderGid { get; set; }
    public FlowEngineShopifyValidationDecision Validation { get; set; } = new();
}

public sealed class FlowEngineShopifyValidateOrdersPayload
{
    public string? Date { get; set; }
    public string? SinceUtc { get; set; }
    public string? UntilUtc { get; set; }
    public bool UseLatestDay { get; set; }
    public string SelectionKind { get; set; } = string.Empty;
    public List<FlowEngineShopifyValidateOrdersDayPayload> Days { get; set; } = new();
}

public sealed class FlowEngineShopifyValidateOrdersDayPayload
{
    public string Date { get; set; } = string.Empty;
    public FlowEngineShopifyValidationCounts Counts { get; set; } = new();
    public List<FlowEngineShopifyValidatedOrderRow> Orders { get; set; } = new();
}

public sealed class FlowEngineShopifyValidationCounts
{
    public int Total { get; set; }
    public int Eligible { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

public sealed class FlowEngineShopifyValidatedOrderRow
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderGid { get; set; }
    public FlowEngineShopifyValidationDecision Validation { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public sealed class FlowEngineShopifyCheckOrdersPayload
{
    public string? Date { get; set; }
    public string? SinceUtc { get; set; }
    public string? UntilUtc { get; set; }
    public bool UseLatestDay { get; set; }
    public string SelectionKind { get; set; } = string.Empty;
    public List<FlowEngineShopifyCheckOrdersDayPayload> Days { get; set; } = new();
}

public sealed class FlowEngineShopifyCheckOrdersDayPayload
{
    public string Date { get; set; } = string.Empty;
    public FlowEngineShopifyCheckCounts Counts { get; set; } = new();
    public List<FlowEngineShopifyCheckedOrderRow> Orders { get; set; } = new();
}

public sealed class FlowEngineShopifyCheckCounts
{
    public int Total { get; set; }
    public int Found { get; set; }
    public int Missing { get; set; }
    public int FailedValidation { get; set; }
    public int Error { get; set; }
}

public sealed class FlowEngineShopifyCheckedOrderRow
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderGid { get; set; }
    public string? ExtOrderNr { get; set; }
    public string? ShopifyFinancialStatus { get; set; }
    public string? ShopifyFulfillmentStatus { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? JeevesOrderStatus { get; set; }
    public int? JeevesOrderNumber { get; set; }
    public string? JeevesStatusName { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FlowEngineShopifySendOrderPayload
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderGid { get; set; }
    public string Status { get; set; } = string.Empty;
    public FlowEngineShopifyValidationDecision Validation { get; set; } = new();
    public FlowEngineShopifyJeevesOrderPayload? Payload { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FlowEngineShopifySendOrdersPayload
{
    public string? Date { get; set; }
    public string? SinceUtc { get; set; }
    public string? UntilUtc { get; set; }
    public bool UseLatestDay { get; set; }
    public string SelectionKind { get; set; } = "date";
    public bool DryRun { get; set; }
    public bool SkipJeevesCheck { get; set; }
    public FlowEngineShopifySendCounts Counts { get; set; } = new();
    public List<FlowEngineShopifySendOrdersDayPayload> Days { get; set; } = new();
}

public sealed class FlowEngineShopifySendOrdersDayPayload
{
    public string Date { get; set; } = string.Empty;
    public FlowEngineShopifySendCounts Counts { get; set; } = new();
    public double RuntimeSeconds { get; set; }
    public List<FlowEngineShopifySendOrderRow> Orders { get; set; } = new();
}

public sealed class FlowEngineShopifySendCounts
{
    public int Total { get; set; }
    public int Mapped { get; set; }
    public int Sent { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedIneligible { get; set; }
    public int Failed { get; set; }
}

public sealed class FlowEngineShopifySendOrderRow
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public string? OrderGid { get; set; }
    public string Status { get; set; } = string.Empty;
    public FlowEngineShopifyValidationDecision? Validation { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FlowEngineShopifyJeevesOrderPayload
{
    public int CompanyCode { get; set; }
    public string? CustomerNumber { get; set; }
    public string ExternalOrderNumber { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public string? OrderDate { get; set; }
    public int OrderType { get; set; }
    public string? CurrencyCode { get; set; }
    public int PartialDeliveryAllowed { get; set; }
    public string Edit { get; set; } = "FlowEngine";
    public string? DeliveryName { get; set; }
    public string? DeliveryAddress1 { get; set; }
    public string? DeliveryAddress2 { get; set; }
    public string? DeliveryZipCode { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryCountryCode { get; set; }
    public string? GoodsMark3 { get; set; }
    public string? GoodsMark4 { get; set; }
    public string? EgenParameter3 { get; set; }
    public List<FlowEngineShopifyJeevesOrderLinePayload> OrderLines { get; set; } = new();
}

public sealed class FlowEngineShopifyJeevesOrderLinePayload
{
    public string? ArticleNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public int CurrencyValue { get; set; }
    public decimal CustomerDiscount { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal? Discount1 { get; set; }
    public decimal? Discount2 { get; set; }
    public decimal? Discount3 { get; set; }
    public string Edit { get; set; } = "FlowEngine";
}

public sealed class FlowEngineCreateShipmentsCounts
{
    public int CentraTotal { get; set; }
    public int Eligible { get; set; }
    public int Successful { get; set; }
    public int SkippedIneligible { get; set; }
    public int Failed { get; set; }
}

public sealed class FlowEngineCreateShipmentResultRow
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ShipmentId { get; set; }
    public bool? IsCaptured { get; set; }
    public bool? IsShipped { get; set; }
    public int? StoreId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<FlowEngineShipmentLineInput> OrderLines { get; set; } = new();
}

public sealed class FlowEngineShipmentLineInput
{
    public string OrderLineId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class FlowEngineJeevesArtStatusRow
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public bool Importable { get; set; }
}
