namespace WebApp.Models.Integration;

public sealed record FlowEngineShopifyOrderReference(string? Gid, string? NumericId);

public sealed record FlowEngineShopifyScopeProbeResult(HashSet<string> Scopes, string? ShopName, string? ShopDomain);

public sealed record FlowEngineShopifyScopeProbeCategory(
    string Category,
    bool IsSatisfied,
    string[] MissingRequiredScopes,
    string[] MissingAnyOfScopes);

public sealed record FlowEngineShopifyDateSelection(DateTime SinceUtc, DateTime UntilUtc, string SelectionKind);

public sealed record FlowEngineShopifyCreateFulfillmentResult(bool Success, string? FulfillmentId, string? ErrorMessage);

public sealed record FlowEngineShopifyCloseOrderResult(bool Success, bool CloseApplied, string? ErrorMessage);

public sealed class FlowEngineShopifyOrderMappingInput
{
    public string? NumericId { get; set; }
    public string? Name { get; set; }
    public string? CreatedAt { get; set; }
    public string? CustomerFirstName { get; set; }
    public string? CustomerLastName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public FlowEngineShopifyOrderAddressMappingInput? ShippingAddress { get; set; }
    public FlowEngineShopifyOrderAddressMappingInput? BillingAddress { get; set; }
    public decimal? FallbackShippingAmount { get; set; }
    public string? FallbackShippingCurrencyCode { get; set; }
    public List<FlowEngineShopifyShippingLineMappingInput> ShippingLines { get; set; } = new();
    public List<FlowEngineShopifyOrderLineMappingInput> OrderLines { get; set; } = new();
}

public sealed class FlowEngineShopifyOrderAddressMappingInput
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? CountryCodeV2 { get; set; }
    public string? Phone { get; set; }
}

public sealed class FlowEngineShopifyShippingLineMappingInput
{
    public decimal? CurrentDiscountedAmount { get; set; }
    public string? CurrentDiscountedCurrencyCode { get; set; }
    public decimal? DiscountedAmount { get; set; }
    public string? DiscountedCurrencyCode { get; set; }
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrencyCode { get; set; }
}

public sealed class FlowEngineShopifyOrderLineMappingInput
{
    public string? Sku { get; set; }
    public string? VariantSku { get; set; }
    public int Quantity { get; set; }
    public decimal? DiscountedTotalAmount { get; set; }
    public string? DiscountedTotalCurrencyCode { get; set; }
    public decimal? OriginalTotalAmount { get; set; }
    public string? OriginalTotalCurrencyCode { get; set; }
}
