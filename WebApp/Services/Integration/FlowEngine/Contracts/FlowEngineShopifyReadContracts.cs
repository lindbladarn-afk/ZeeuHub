namespace WebApp.Services.Integration.FlowEngine;

public sealed record FlowEngineShopifyCollectProductsResult(List<ShopifyProductNode> Products, bool HasNextPage, string? EndCursor);

public sealed class ShopifyGetProductsData
{
    public ShopifyProductsConnection? Products { get; set; }
}

public sealed class ShopifyProductsConnection
{
    public ShopifyPageInfo? PageInfo { get; set; }
    public List<ShopifyProductEdge>? Edges { get; set; }
}

public sealed class ShopifyProductEdge
{
    public ShopifyProductNode? Node { get; set; }
}

public sealed class ShopifyProductNode
{
    public string? Id { get; set; }
    public string? LegacyResourceId { get; set; }
    public string? Title { get; set; }
    public string? Handle { get; set; }
    public string? Status { get; set; }
    public string? Vendor { get; set; }
    public string? ProductType { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public ShopifyProductVariantConnection? Variants { get; set; }
    public List<ShopifyProductOption> Options { get; set; } = new();
    public ShopifyProductImageConnection? Images { get; set; }
    public ShopifyProductMetafieldConnection? Metafields { get; set; }
}

public sealed class ShopifyProductVariantConnection
{
    public List<ShopifyProductVariantEdge> Edges { get; set; } = new();
}

public sealed class ShopifyProductVariantEdge
{
    public ShopifyProductVariantNode? Node { get; set; }
}

public sealed class ShopifyProductVariantNode
{
    public string? Id { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Title { get; set; }
    public string? Price { get; set; }
    public string? CompareAtPrice { get; set; }
    public ShopifyInventoryItem? InventoryItem { get; set; }
}

public sealed class ShopifyInventoryItem
{
    public string? Id { get; set; }
    public bool? Tracked { get; set; }
}

public sealed class ShopifyProductOption
{
    public string? Name { get; set; }
    public List<string> Values { get; set; } = new();
}

public sealed class ShopifyProductImageConnection
{
    public List<ShopifyProductImageEdge> Edges { get; set; } = new();
}

public sealed class ShopifyProductImageEdge
{
    public ShopifyProductImageNode? Node { get; set; }
}

public sealed class ShopifyProductImageNode
{
    public string? Url { get; set; }
    public string? AltText { get; set; }
}

public sealed class ShopifyProductMetafieldConnection
{
    public List<ShopifyProductMetafieldEdge> Edges { get; set; } = new();
}

public sealed class ShopifyProductMetafieldEdge
{
    public ShopifyProductMetafieldNode? Node { get; set; }
}

public sealed class ShopifyProductMetafieldNode
{
    public string? Namespace { get; set; }
    public string? Key { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class ShopifyFetchOrderData
{
    public ShopifyOrderDetailNode? Order { get; set; }
}

public sealed class ShopifyFetchOrdersData
{
    public ShopifySummaryOrdersConnection? Orders { get; set; }
}

public sealed class ShopifyTagsAddData
{
    public ShopifyTagsAddPayload? TagsAdd { get; set; }
}

public sealed class ShopifyTagsAddPayload
{
    public List<ShopifyMutationUserError>? UserErrors { get; set; }
}

public sealed class ShopifyMutationUserError
{
    public List<string>? Field { get; set; }
    public string? Message { get; set; }
}

public sealed class ShopifyValidateOrdersByDateData
{
    public ShopifyDetailOrdersConnection? Orders { get; set; }
}

public sealed class ShopifySummaryOrdersConnection
{
    public ShopifyPageInfo? PageInfo { get; set; }
    public List<ShopifySummaryOrderEdge>? Edges { get; set; }
}

public sealed class ShopifySummaryOrderEdge
{
    public ShopifyOrderSummaryNode? Node { get; set; }
}

public sealed class ShopifyDetailOrdersConnection
{
    public ShopifyPageInfo? PageInfo { get; set; }
    public List<ShopifyDetailOrderEdge>? Edges { get; set; }
}

public sealed class ShopifyDetailOrderEdge
{
    public ShopifyOrderDetailNode? Node { get; set; }
}

public sealed class ShopifyPageInfo
{
    public bool HasNextPage { get; set; }
    public string? EndCursor { get; set; }
}

public class ShopifyOrderSummaryNode
{
    public string? Id { get; set; }
    public string? LegacyResourceId { get; set; }
    public string? Name { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? CancelledAt { get; set; }
    public bool? Test { get; set; }
    public string? DisplayFinancialStatus { get; set; }
    public string? DisplayFulfillmentStatus { get; set; }
}

public sealed class ShopifyOrderDetailNode : ShopifyOrderSummaryNode
{
    public ShopifyCustomerNode? Customer { get; set; }
    public ShopifyAddressNode? BillingAddress { get; set; }
    public ShopifyAddressNode? ShippingAddress { get; set; }
    public ShopifyMoneySetNode? TotalShippingPriceSet { get; set; }
    public ShopifyShippingLinesConnection? ShippingLines { get; set; }
    public ShopifyOrderLineItemsConnection? LineItems { get; set; }
}

public sealed class ShopifyCustomerNode
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public sealed class ShopifyAddressNode
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Zip { get; set; }
    public string? CountryCodeV2 { get; set; }
    public string? Phone { get; set; }
}

public sealed class ShopifyMoneySetNode
{
    public ShopifyMoneyNode? ShopMoney { get; set; }
}

public sealed class ShopifyMoneyNode
{
    public string? Amount { get; set; }
    public string? CurrencyCode { get; set; }
}

public sealed class ShopifyShippingLinesConnection
{
    public List<ShopifyShippingLineEdge>? Edges { get; set; }
}

public sealed class ShopifyShippingLineEdge
{
    public ShopifyShippingLineNode? Node { get; set; }
}

public sealed class ShopifyShippingLineNode
{
    public string? Title { get; set; }
    public string? Code { get; set; }
    public ShopifyMoneySetNode? OriginalPriceSet { get; set; }
    public ShopifyMoneySetNode? DiscountedPriceSet { get; set; }
    public ShopifyMoneySetNode? CurrentDiscountedPriceSet { get; set; }
}

public sealed class ShopifyOrderLineItemsConnection
{
    public ShopifyPageInfo? PageInfo { get; set; }
    public List<ShopifyOrderLineItemEdge>? Edges { get; set; }
}

public sealed class ShopifyOrderLineItemEdge
{
    public ShopifyOrderLineItemNode? Node { get; set; }
}

public sealed class ShopifyOrderLineItemNode
{
    public string? Id { get; set; }
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
    public ShopifyMoneySetNode? OriginalTotalSet { get; set; }
    public ShopifyMoneySetNode? DiscountedTotalSet { get; set; }
    public ShopifyOrderLineItemVariantNode? Variant { get; set; }
}

public sealed class ShopifyOrderLineItemVariantNode
{
    public string? Id { get; set; }
    public string? Sku { get; set; }
    public string? Title { get; set; }
    public string? LegacyResourceId { get; set; }
}
