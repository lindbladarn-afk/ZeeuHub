using System.Collections.Generic;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraSendOrdersRowBuilder
{
    public static FlowEngineSendOrdersRow Create(
        CentraSendOrdersContracts.CentraRawOrder order,
        string status,
        string? errorMessage,
        string? payloadJson,
        List<FlowEngineSendOrdersRuleFailure> validationFailures,
        List<FlowEngineSendOrdersRuleFailure> eligibilityFailures)
    {
        return new FlowEngineSendOrdersRow
        {
            Id = order.Id,
            Number = order.Number ?? string.Empty,
            CreatedAt = order.CreatedAt ?? string.Empty,
            Status = status,
            ErrorMessage = NormalizeOptional(errorMessage),
            PayloadJson = NormalizeOptional(payloadJson),
            ValidationFailures = validationFailures,
            EligibilityFailures = eligibilityFailures
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
