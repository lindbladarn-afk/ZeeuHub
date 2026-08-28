using WebApp.ViewModels.Shared;

namespace WebApp.Models.Integration;

public sealed class FlowEngineModuleOptions
{
    public const string SectionName = "FlowEngine";

    public bool Enabled { get; set; }
    public string Title { get; set; } = "FlowEngine";
    public string Subtitle { get; set; } = "Portal-native omskrivning av FlowEngine i C# och Razor.";
    public string Summary { get; set; } = "ZeeU Portal ska äga UI, auth, jobb och integrationer utan extern Swift-app eller separat URL.";
    public string MigrationPhase { get; set; } = "scaffold";
    public int DocumentExtractMaxBytes { get; set; } = 10 * 1024 * 1024;
}

public enum FlowEngineModuleReadiness
{
    Planned = 0,
    InProgress = 1,
    Available = 2
}

public sealed class FlowEngineOperationDescriptor
{
    public string Key { get; set; } = string.Empty;
    public FlowEngineOperationType Operation { get; set; }
    public string Section { get; set; } = FlowEngineSectionKeys.Dashboard;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Slice { get; set; } = string.Empty;
    public FlowEngineModuleReadiness Readiness { get; set; } = FlowEngineModuleReadiness.Planned;
}

public sealed class FlowEngineOperationDefinition
{
    public string Key { get; set; } = string.Empty;
    public FlowEngineOperationType Operation { get; set; }
    public string Section { get; set; } = FlowEngineSectionKeys.Dashboard;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Slice { get; set; } = string.Empty;
    public FlowEngineModuleReadiness Readiness { get; set; } = FlowEngineModuleReadiness.Planned;
}

public sealed class FlowEngineNativeVerticalDescriptor
{
    public string Title { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Why { get; set; } = string.Empty;
}

public static class FlowEngineSectionKeys
{
    public const string Dashboard = "dashboard";
    public const string Jeeves = "jeeves";
    public const string Centra = "centra";
    public const string Shopify = "shopify";
    public const string Akeneo = "akeneo";
    public const string Jobs = "jobs";
    public const string Config = "config";

    public static string Normalize(string? section)
    {
        return section?.Trim().ToLowerInvariant() switch
        {
            Jeeves => Jeeves,
            Centra => Centra,
            Shopify => Shopify,
            Akeneo => Akeneo,
            Jobs => Jobs,
            Config => Config,
            _ => Dashboard
        };
    }

    public static string ToAnchorId(string section)
    {
        return Normalize(section) switch
        {
            Jeeves => "flowengine-section-jeeves",
            Centra => "flowengine-section-centra",
            Shopify => "flowengine-section-shopify",
            Akeneo => "flowengine-section-akeneo",
            Jobs => "flowengine-section-jobs",
            Config => "flowengine-section-config",
            _ => "flowengine-section-dashboard"
        };
    }
}

public sealed class FlowEngineRunCustomerAddressesInput
{
    public string CustomerNumber { get; set; } = string.Empty;
}

public sealed class FlowEngineRunConfigValidateInput
{
}

public sealed class FlowEngineRunGetOrdersInput
{
    public int? CompanyCode { get; set; }
    public string LookupField { get; set; } = "c_extordernr";
    public string LookupValue { get; set; } = string.Empty;
}

public sealed class FlowEngineRunOrderExistsInput
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class FlowEngineRunCheckOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunCentraFetchOrderInput
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class FlowEngineRunCentraFetchOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
}

public sealed class FlowEngineRunCentraFetchReturnInput
{
    public string ReturnId { get; set; } = string.Empty;
}

public sealed class FlowEngineRunCentraFetchReturnsInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
}

public sealed class FlowEngineRunSendOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
    public bool SkipJeevesCheck { get; set; }
}

public sealed class FlowEngineRunSendOrderInput
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
    public bool SkipJeevesCheck { get; set; }
}

public sealed class FlowEngineRunSendReturnsInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineRunSendReturnInput
{
    public string ReturnId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineRunCreateShipmentsInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineRunCreateShipmentsPendingInput
{
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineRunCreateShipmentInput
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineRunCompleteOrdersPendingInput
{
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
    public bool CloseOrder { get; set; }
}

public sealed class FlowEngineRunCompleteOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
    public bool CloseOrder { get; set; }
}

public sealed class FlowEngineRunCompleteOrderInput
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
    public bool CloseOrder { get; set; }
}

public sealed class FlowEngineRunShopifyScopesCheckInput
{
}

public sealed class FlowEngineRunShopifyGetProductsInput
{
    public string Query { get; set; } = string.Empty;
    public string UpdatedSince { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunShopifyFetchOrderInput
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class FlowEngineRunShopifyFetchOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunShopifyValidateOrderInput
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class FlowEngineRunShopifyValidateOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunShopifyCheckOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunShopifySendOrderInput
{
    public string OrderId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
    public bool SkipJeevesCheck { get; set; }
}

public sealed class FlowEngineRunShopifySendOrdersInput
{
    public string DateUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string SinceUtc { get; set; } = string.Empty;
    public string UntilUtc { get; set; } = string.Empty;
    public bool UseLatestDay { get; set; }
    public bool ForceRange { get; set; }
    public int? Limit { get; set; }
    public bool DryRun { get; set; } = true;
    public bool SkipJeevesCheck { get; set; }
}

public sealed class FlowEngineRunAkeneoProductsInput
{
    public string Skus { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunAkeneoAllProductsInput
{
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunAkeneoSendToShopifyInput
{
    public string Sku { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunAkeneoSendToCentraInput
{
    public string Sku { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class FlowEngineRunProductInput
{
    public string ArticleNumber { get; set; } = string.Empty;
}

public sealed class FlowEngineRunArtStatusInput
{
    public string ArticleNumbers { get; set; } = string.Empty;
}

public sealed class FlowEngineRunImportOrderInput
{
    public string CustomerNumber { get; set; } = string.Empty;
    public int OrderType { get; set; } = 1;
    public string CustomerReference { get; set; } = string.Empty;
    public string ExternalOrderNumber { get; set; } = string.Empty;
    public string DeliveryPlaceCode { get; set; } = string.Empty;
    public string Lines { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
}

public sealed class FlowEngineDeliveryAddressOption
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FtgNamn { get; set; } = string.Empty;
}

public sealed class FlowEngineImportAddressLookupContext
{
    public int CompanyCode { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
}

public sealed class FlowEngineImportOrderSessionState
{
    public FlowEngineRunImportOrderInput Form { get; set; } = new();
    public List<FlowEngineDeliveryAddressOption> DeliveryAddressOptions { get; set; } = new();
    public FlowEngineImportAddressLookupContext? AddressLookupContext { get; set; }
    public FlowEngineImportDocumentReview? DocumentReview { get; set; }
    public List<FlowEngineJeevesArtStatusRow> ArtStatusRows { get; set; } = new();
}

public sealed class FlowEngineOrderDocumentInput
{
    public string FileName { get; set; } = string.Empty;
    public string? MediaType { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public sealed class FlowEngineOrderDocumentExtractionLine
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
}

public sealed class FlowEngineOrderDocumentExtractionSource
{
    public string Kind { get; set; } = "deterministic";
    public string Label { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class FlowEngineOrderDocumentExtractionResult
{
    public IReadOnlyList<FlowEngineOrderDocumentExtractionLine> Lines { get; set; } = Array.Empty<FlowEngineOrderDocumentExtractionLine>();
    public FlowEngineOrderDocumentExtractionSource Source { get; set; } = new();
}

public sealed class FlowEngineImportDocumentReviewLine
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
}

public sealed class FlowEngineImportDocumentReview
{
    public string FileName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public FlowEngineOrderDocumentExtractionSource? Source { get; set; }
    public List<FlowEngineImportDocumentReviewLine> Lines { get; set; } = new();
}

public sealed class FlowEngineWorkbenchSettingsState
{
    public bool TestMode { get; set; } = true;
    public bool DryRun { get; set; }
    public bool DebugHttp { get; set; }
    public bool SkipJeevesCheck { get; set; }
    public bool UseLimit { get; set; } = true;
    public int Limit { get; set; } = 10;
    public bool CentraSchedulerEnabled { get; set; }
    public bool ShopifySchedulerEnabled { get; set; }
}

public sealed class FlowEngineSystemStatusViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

public sealed class FlowEngineWorkbenchFormsViewModel
{
    public FlowEngineRunCheckOrdersInput CheckOrders { get; set; } = new();
    public FlowEngineRunConfigValidateInput ConfigValidate { get; set; } = new();
    public FlowEngineRunCentraFetchOrderInput CentraFetchOrder { get; set; } = new();
    public FlowEngineRunCentraFetchOrdersInput CentraFetchOrders { get; set; } = new();
    public FlowEngineRunCentraFetchReturnInput CentraFetchReturn { get; set; } = new();
    public FlowEngineRunCentraFetchReturnsInput CentraFetchReturns { get; set; } = new();
    public FlowEngineRunSendOrderInput SendOrder { get; set; } = new();
    public FlowEngineRunSendOrdersInput SendOrders { get; set; } = new();
    public FlowEngineRunSendReturnInput SendReturn { get; set; } = new();
    public FlowEngineRunSendReturnsInput SendReturns { get; set; } = new();
    public FlowEngineRunCreateShipmentsInput CreateShipments { get; set; } = new();
    public FlowEngineRunCreateShipmentsPendingInput CreateShipmentsPending { get; set; } = new();
    public FlowEngineRunCreateShipmentInput CreateShipment { get; set; } = new();
    public FlowEngineRunCompleteOrdersPendingInput CompleteOrdersPending { get; set; } = new();
    public FlowEngineRunCompleteOrdersInput CompleteOrders { get; set; } = new();
    public FlowEngineRunCompleteOrderInput CompleteOrder { get; set; } = new();
    public FlowEngineRunShopifyScopesCheckInput ShopifyScopesCheck { get; set; } = new();
    public FlowEngineRunShopifyGetProductsInput ShopifyGetProducts { get; set; } = new();
    public FlowEngineRunShopifyFetchOrderInput ShopifyFetchOrder { get; set; } = new();
    public FlowEngineRunShopifyFetchOrdersInput ShopifyFetchOrders { get; set; } = new();
    public FlowEngineRunShopifyValidateOrderInput ShopifyValidateOrder { get; set; } = new();
    public FlowEngineRunShopifyValidateOrdersInput ShopifyValidateOrders { get; set; } = new();
    public FlowEngineRunShopifyCheckOrdersInput ShopifyCheckOrders { get; set; } = new();
    public FlowEngineRunShopifySendOrderInput ShopifySendOrder { get; set; } = new();
    public FlowEngineRunShopifySendOrdersInput ShopifySendOrders { get; set; } = new();
    public FlowEngineRunAkeneoProductsInput AkeneoProducts { get; set; } = new();
    public FlowEngineRunAkeneoAllProductsInput AkeneoAllProducts { get; set; } = new();
    public FlowEngineRunAkeneoSendToShopifyInput AkeneoSendToShopify { get; set; } = new();
    public FlowEngineRunAkeneoSendToCentraInput AkeneoSendToCentra { get; set; } = new();
    public FlowEngineRunCustomerAddressesInput CustomerAddresses { get; set; } = new();
    public FlowEngineRunGetOrdersInput GetOrders { get; set; } = new();
    public FlowEngineRunOrderExistsInput OrderExists { get; set; } = new();
    public FlowEngineRunProductInput Product { get; set; } = new();
    public FlowEngineRunArtStatusInput ArtStatus { get; set; } = new();
    public FlowEngineRunImportOrderInput ImportOrder { get; set; } = new();
}

public sealed class FlowEngineModuleViewModel
{
    public string Title { get; set; } = "FlowEngine";
    public string Subtitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string MigrationPhase { get; set; } = string.Empty;
    public string ActiveSection { get; set; } = FlowEngineSectionKeys.Dashboard;
    public ModuleBannerViewModel? Banner { get; set; }
    public bool CanRunReadOperations { get; set; }
    public string? RuntimeCompanyName { get; set; }
    public int? RuntimeCompanyCode { get; set; }
    public FlowEngineWorkbenchFormsViewModel Forms { get; set; } = new();
    public IReadOnlyList<FlowEngineDeliveryAddressOption> ImportDeliveryAddressOptions { get; set; } = Array.Empty<FlowEngineDeliveryAddressOption>();
    public FlowEngineImportAddressLookupContext? ImportAddressLookupContext { get; set; }
    public FlowEngineImportDocumentReview? ImportDocumentReview { get; set; }
    public IReadOnlyList<FlowEngineJeevesArtStatusRow> ImportArtStatusRows { get; set; } = Array.Empty<FlowEngineJeevesArtStatusRow>();
    public FlowEngineWorkbenchSettingsState WorkbenchSettings { get; set; } = new();
    public IReadOnlyList<FlowEngineSystemStatusViewModel> SystemStatuses { get; set; } = Array.Empty<FlowEngineSystemStatusViewModel>();
    public IReadOnlyList<FlowEngineOperationDescriptor> Operations { get; set; } = Array.Empty<FlowEngineOperationDescriptor>();
    public IReadOnlyList<FlowEngineNativeVerticalDescriptor> Verticals { get; set; } = Array.Empty<FlowEngineNativeVerticalDescriptor>();
    public IReadOnlyList<FlowEngineJobSnapshot> RecentJobs { get; set; } = Array.Empty<FlowEngineJobSnapshot>();
    public FlowEngineJobSnapshot? SelectedJob { get; set; }
    public FlowEngineHistoryPageResult DashboardHistory { get; set; } = new();
    public FlowEngineHistoryPageResult JeevesHistory { get; set; } = new();
    public FlowEngineHistoryPageResult CentraHistory { get; set; } = new();
    public FlowEngineHistoryPageResult ShopifyHistory { get; set; } = new();
    public FlowEngineHistoryPageResult AkeneoHistory { get; set; } = new();
}
