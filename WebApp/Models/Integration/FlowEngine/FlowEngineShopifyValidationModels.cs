namespace WebApp.Models.Integration;

public sealed class FlowEngineShopifyOrderValidationInput
{
    public string? NumericId { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsTest { get; set; }
    public string? DisplayFinancialStatus { get; set; }
    public string? DisplayFulfillmentStatus { get; set; }
    public string? CustomerFirstName { get; set; }
    public string? CustomerLastName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public FlowEngineShopifyAddressValidationInput? ShippingAddress { get; set; }
    public FlowEngineShopifyAddressValidationInput? BillingAddress { get; set; }
    public bool HasLineWithoutSku { get; set; }
    public decimal? ShippingAmount { get; set; }
    public string? ShippingCurrencyCode { get; set; }
}

public sealed class FlowEngineShopifyAddressValidationInput
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Address1 { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? CountryCodeV2 { get; set; }
}
