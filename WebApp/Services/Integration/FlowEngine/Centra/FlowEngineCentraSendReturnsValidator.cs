using System;
using System.Collections.Generic;
using System.Linq;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraSendReturnsValidator
{
    public static FlowEngineCentraSendReturnsValidationResult Validate(CentraSendReturnsContracts.CentraRawReturn returnData)
    {
        var validationFailures = new List<FlowEngineSendReturnsRuleFailure>();
        var eligibilityFailures = new List<FlowEngineSendReturnsRuleFailure>();

        if (returnData.Id <= 0)
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_id", Message = "Return id is required" });

        if ((returnData.Store?.Id ?? 0) <= 0)
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_store", Message = "Store id is required" });

        var hasProductLines = returnData.Lines.Count > 0;
        var hasAdditionalChargeLines = HasReturnAdditionalChargeLines(returnData.Totals);
        if (!hasProductLines && !hasAdditionalChargeLines)
        {
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_lines", Message = "Return must contain at least one line" });
        }

        if (returnData.Lines.Any(line => string.IsNullOrWhiteSpace(line.OrderLine?.ProductVariant?.VariantNumber)))
        {
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_product", Message = "Return contains line(s) with missing product information" });
        }

        if (returnData.Lines.Any(line => (line.OrderLine?.ReturnedQuantity ?? 0m) <= 0m))
        {
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "invalid_quantity", Message = "Return contains line(s) with invalid quantity" });
        }

        if (string.IsNullOrWhiteSpace(returnData.Order?.Id))
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_order_reference", Message = "Return is missing order reference" });

        if (string.IsNullOrWhiteSpace(returnData.Order?.Number))
            validationFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "missing_order_number", Message = "Return is missing original order number" });

        if (!string.Equals(returnData.ReturnStatus?.Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            eligibilityFailures.Add(new FlowEngineSendReturnsRuleFailure { Code = "ineligible_status", Message = "Return status must be COMPLETED" });
        }

        return new FlowEngineCentraSendReturnsValidationResult(validationFailures, eligibilityFailures);
    }

    private static bool HasReturnAdditionalChargeLines(CentraSendReturnsContracts.CentraReturnTotals? totals)
    {
        if (totals is null)
            return false;

        return (totals.Shipping?.Value ?? 0m) != 0m ||
               (totals.Handling?.Value ?? 0m) != 0m ||
               (totals.Discounts?.Value ?? 0m) != 0m ||
               (totals.ReturnCost?.Value ?? 0m) > 0m;
    }
}

internal sealed class FlowEngineCentraSendReturnsValidationResult
{
    public FlowEngineCentraSendReturnsValidationResult(
        List<FlowEngineSendReturnsRuleFailure> validationFailures,
        List<FlowEngineSendReturnsRuleFailure> eligibilityFailures)
    {
        ValidationFailures = validationFailures;
        EligibilityFailures = eligibilityFailures;
    }

    public List<FlowEngineSendReturnsRuleFailure> ValidationFailures { get; }
    public List<FlowEngineSendReturnsRuleFailure> EligibilityFailures { get; }
}
