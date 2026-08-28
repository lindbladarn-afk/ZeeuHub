namespace WebApp.Services.ExcelImport;

// Holds template-specific settings for the shared supplier-price edit adapter.
public sealed class SupplierPriceEditSessionDefinition
{
    public required string ImportType { get; init; }
    public required string EditSessionFileName { get; init; }
    public required int MaxEditableRows { get; init; }
    public required string MissingStagingTableMessage { get; init; }
    public required string ValidationStoppedBeforeStagingMessage { get; init; }
}
