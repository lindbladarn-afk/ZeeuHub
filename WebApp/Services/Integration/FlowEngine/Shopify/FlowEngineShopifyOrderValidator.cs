using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyOrderValidator : IFlowEngineShopifyOrderValidator
{
    public FlowEngineShopifyValidationDecision Validate(FlowEngineShopifyOrderValidationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.NumericId))
        {
            return FailedDecision(
                "SHP-VAL-002",
                "numericId cannot be resolved from GID or legacyResourceId",
                "Ensure order payload includes valid gid://shopify/Order/<id> or legacyResourceId");
        }

        if (input.IsCancelled)
        {
            return SkippedDecision(
                "SHP-VAL-006",
                "Order is cancelled",
                "Exclude cancelled orders from send/complete batches");
        }

        if (input.IsTest)
        {
            return SkippedDecision(
                "SHP-VAL-007",
                "Order is marked as test",
                "Exclude test orders from production send/complete");
        }

        if (!string.Equals(Normalize(input.DisplayFinancialStatus), "PAID", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Normalize(input.DisplayFulfillmentStatus), "UNFULFILLED", StringComparison.OrdinalIgnoreCase))
        {
            return SkippedDecision(
                "SHP-VAL-008",
                "Order is not send-eligible (requires displayFinancialStatus=PAID and displayFulfillmentStatus=UNFULFILLED)",
                "Send only orders that are paid and still unfulfilled");
        }

        var customerHasName = !string.IsNullOrWhiteSpace(input.CustomerFirstName) || !string.IsNullOrWhiteSpace(input.CustomerLastName);
        var customerHasContact = !string.IsNullOrWhiteSpace(input.CustomerEmail) || !string.IsNullOrWhiteSpace(input.CustomerPhone);
        if (!customerHasName || !customerHasContact)
        {
            return SkippedDecision(
                "SHP-VAL-009",
                "Required customer identity fields are missing",
                "Fix customer data in Shopify before retry");
        }

        if (IsMissingAddressShape(input.ShippingAddress) || IsMissingAddressShape(input.BillingAddress))
        {
            return SkippedDecision(
                "SHP-VAL-010",
                "Required shipping/billing address fields are missing",
                "Fix address data in Shopify before retry");
        }

        if (input.HasLineWithoutSku)
        {
            return SkippedDecision(
                "SHP-VAL-011",
                "At least one required shippable line is missing SKU/external mapping key",
                "Correct SKU/mapping data and rerun");
        }

        if (!input.ShippingAmount.HasValue || string.IsNullOrWhiteSpace(input.ShippingCurrencyCode))
        {
            return FailedDecision(
                "SHP-VAL-012",
                "Shipping totals are missing (requires totalShippingPriceSet.shopMoney.amount and currencyCode)",
                "Ensure Shopify order exposes totalShippingPriceSet.shopMoney before sending");
        }

        return EligibleDecision();
    }

    private static bool IsMissingAddressShape(FlowEngineShopifyAddressValidationInput? address)
    {
        return address is null ||
               string.IsNullOrWhiteSpace(address.FirstName) ||
               string.IsNullOrWhiteSpace(address.LastName) ||
               string.IsNullOrWhiteSpace(address.Address1) ||
               string.IsNullOrWhiteSpace(address.City) ||
               string.IsNullOrWhiteSpace(address.Zip) ||
               string.IsNullOrWhiteSpace(address.CountryCodeV2);
    }

    private static FlowEngineShopifyValidationDecision EligibleDecision()
        => new() { Status = "eligible", Classification = "pass", Message = "Order is eligible for send/complete" };

    private static FlowEngineShopifyValidationDecision SkippedDecision(string ruleId, string message, string remediation)
        => new()
        {
            Status = "skipped",
            Classification = "business_rule",
            RuleId = ruleId,
            Message = message,
            Remediation = remediation
        };

    private static FlowEngineShopifyValidationDecision FailedDecision(string ruleId, string message, string remediation)
        => new()
        {
            Status = "failed",
            Classification = "data_error",
            RuleId = ruleId,
            Message = message,
            Remediation = remediation
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
