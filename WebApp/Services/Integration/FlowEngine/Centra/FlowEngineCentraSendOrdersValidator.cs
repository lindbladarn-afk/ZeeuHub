using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraSendOrdersValidator
{
    public static FlowEngineCentraSendOrdersValidationResult Validate(CentraSendOrdersContracts.CentraRawOrder order)
    {
        var validationFailures = new List<FlowEngineSendOrdersRuleFailure>();
        var eligibilityFailures = new List<FlowEngineSendOrdersRuleFailure>();

        if (string.IsNullOrWhiteSpace(order.Id))
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure { Code = "missing_id", Message = "Order id is required" });

        if (order.Lines.Count == 0)
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure { Code = "missing_lines", Message = "Order must contain at least one line" });

        if (order.ShippingAddress is null)
        {
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure { Code = "missing_shipping_address", Message = "Shipping address is required" });
        }
        else if (string.IsNullOrWhiteSpace(order.ShippingAddress.Email) && string.IsNullOrWhiteSpace(order.ShippingAddress.PhoneNumber))
        {
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure { Code = "missing_contact", Message = "Email or phone number is required" });
        }

        if (string.IsNullOrWhiteSpace(order.GrandTotal?.Currency?.Code))
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure { Code = "missing_currency", Message = "Currency code is required" });

        if (order.Store?.Id == 2 && string.IsNullOrWhiteSpace(FlowEngineCentraStoreConfigService.GetOrderCustomerNumber(order)))
        {
            validationFailures.Add(new FlowEngineSendOrdersRuleFailure
            {
                Code = "missing_customer_number",
                Message = "Customer number is required for store 2"
            });
        }

        var status = order.Status?.Trim().ToUpperInvariant();
        if (!string.Equals(status, "CONFIRMED", StringComparison.Ordinal) &&
            !string.Equals(status, "SHIPPED", StringComparison.Ordinal))
        {
            eligibilityFailures.Add(new FlowEngineSendOrdersRuleFailure
            {
                Code = "status_not_eligible",
                Message = "Order status must be CONFIRMED or SHIPPED"
            });
        }

        if (order.Store?.Id == 1)
        {
            var authEntry = order.PaymentHistory.FirstOrDefault(entry =>
                string.Equals(entry.EntryType?.Trim(), "AUTH", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase));

            if (authEntry is null)
            {
                eligibilityFailures.Add(new FlowEngineSendOrdersRuleFailure
                {
                    Code = "missing_auth",
                    Message = "Store 1 order requires successful payment authorization (AUTH with status SUCCESS)"
                });
            }
            else if (authEntry.Value?.Value != order.GrandTotal?.Value)
            {
                validationFailures.Add(new FlowEngineSendOrdersRuleFailure
                {
                    Code = "auth_total_mismatch",
                    Message = $"Payment auth amount ({FormatDecimalForMessage(authEntry.Value?.Value)}) does not match grand total ({FormatDecimalForMessage(order.GrandTotal?.Value)})"
                });
            }
        }

        return new FlowEngineCentraSendOrdersValidationResult(validationFailures, eligibilityFailures);
    }

    private static string FormatDecimalForMessage(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
}

internal sealed class FlowEngineCentraSendOrdersValidationResult
{
    public FlowEngineCentraSendOrdersValidationResult(
        List<FlowEngineSendOrdersRuleFailure> validationFailures,
        List<FlowEngineSendOrdersRuleFailure> eligibilityFailures)
    {
        ValidationFailures = validationFailures;
        EligibilityFailures = eligibilityFailures;
    }

    public List<FlowEngineSendOrdersRuleFailure> ValidationFailures { get; }
    public List<FlowEngineSendOrdersRuleFailure> EligibilityFailures { get; }
}
