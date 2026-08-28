using System.Collections.Generic;
using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraSendReturnsRowBuilder
{
    public static FlowEngineSendReturnsRow Create(
        CentraSendReturnsContracts.CentraRawReturn returnData,
        string status,
        string? errorMessage,
        List<FlowEngineSendReturnsRuleFailure> validationFailures,
        List<FlowEngineSendReturnsRuleFailure> eligibilityFailures)
    {
        return new FlowEngineSendReturnsRow
        {
            Id = returnData.Id.ToString(CultureInfo.InvariantCulture),
            OrderId = returnData.Order?.Id ?? string.Empty,
            OrderNumber = returnData.Order?.Number ?? string.Empty,
            CreatedAt = returnData.CreatedAt ?? string.Empty,
            Status = status,
            ErrorMessage = NormalizeOptional(errorMessage),
            ValidationFailures = validationFailures,
            EligibilityFailures = eligibilityFailures
        };
    }

    public static FlowEngineSendReturnsRow Copy(FlowEngineSendReturnsRow source, string status, string? errorMessage)
    {
        return new FlowEngineSendReturnsRow
        {
            Id = source.Id,
            OrderId = source.OrderId,
            OrderNumber = source.OrderNumber,
            CreatedAt = source.CreatedAt,
            Status = status,
            ErrorMessage = errorMessage,
            ValidationFailures = source.ValidationFailures,
            EligibilityFailures = source.EligibilityFailures
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
