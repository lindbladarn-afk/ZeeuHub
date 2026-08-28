using WebApp.Services.Integration.FlowEngine.CentraSendReturnsContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraReturnPaymentInfoHelper
{
    public static ReturnPaymentInfo ExtractReturnPaymentInfo(CentraRawReturn returnData)
    {
        return new ReturnPaymentInfo
        {
            MerchantReference = FirstNonEmpty(
                ExtractFromOrderPaymentHistory(returnData.Order?.PaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "MerchantReference", "merchantReference")),
                ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "MerchantReference", "merchantReference")),
                IsAdyen(returnData.Order?.PaymentMethod?.Name) ? PaymentReference(returnData) : null),
            PaymentReference = PaymentReference(returnData),
            PaymentMethod = FirstNonEmpty(
                returnData.RefundPaymentHistory.Select(entry => NormalizeOptional(entry.PaymentMethod)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                NormalizeOptional(returnData.Order?.PaymentMethod?.Name),
                ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "paymentMethod", "PaymentMethod"))),
            PaymentMethodName = FirstNonEmpty(
                ExtractFromOrderPaymentHistory(returnData.Order?.PaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractNestedProperty(json, "PaymentMethod", "PaymentMethodName")),
                ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, json =>
                {
                    var eventCode = FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "eventCode", "EventCode");
                    return string.Equals(eventCode, "REFUND", StringComparison.OrdinalIgnoreCase)
                        ? FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "paymentMethod", "PaymentMethod")
                        : null;
                }),
                ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractNestedProperty(json, "PaymentMethod", "PaymentMethodName")))
        };
    }

    private static string? PaymentReference(CentraRawReturn returnData)
        => FirstNonEmpty(
            ExtractFromOrderPaymentHistory(returnData.Order?.PaymentHistory, PaymentTransactionId),
            ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, PaymentTransactionId),
            ExtractExternalReference(returnData.Order?.PaymentHistory),
            ExtractFromRefundPaymentHistory(returnData.RefundPaymentHistory, json => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "pspReference")));

    private static string? PaymentTransactionId(string? json)
        => FirstNonEmpty(
            FlowEngineCentraJsonElementReader.ExtractFirstArrayObjectPropertyAsString(json, "PaymentTransactions", "PaymentTransactionId"),
            FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "PaymentTransactionId", "paymentTransactionId"),
            FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(json, "PaymentReference", "paymentReference"));

    private static string? ExtractFromOrderPaymentHistory(
        IReadOnlyList<CentraPaymentHistory>? history,
        Func<string?, string?> extractor)
    {
        if (history is null || history.Count == 0)
            return null;

        foreach (var entry in PrioritizedOrderPaymentHistory(history))
        {
            var value = NormalizeOptional(extractor(entry.ParamsJson));
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ExtractExternalReference(IReadOnlyList<CentraPaymentHistory>? history)
    {
        if (history is null || history.Count == 0)
            return null;

        foreach (var entry in PrioritizedOrderPaymentHistory(history))
        {
            var value = NormalizeOptional(entry.ExternalReference);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IEnumerable<CentraPaymentHistory> PrioritizedOrderPaymentHistory(IReadOnlyList<CentraPaymentHistory> history)
    {
        var refundEntries = new List<CentraPaymentHistory>();
        var successfulEntries = new List<CentraPaymentHistory>();
        var remainingEntries = new List<CentraPaymentHistory>();

        foreach (var entry in history)
        {
            if (IsRefundEntry(entry.EntryType) && IsSuccessOrUnknown(entry.Status))
            {
                refundEntries.Add(entry);
                continue;
            }

            if (IsSuccessOrUnknown(entry.Status))
            {
                successfulEntries.Add(entry);
                continue;
            }

            remainingEntries.Add(entry);
        }

        return refundEntries.Concat(successfulEntries).Concat(remainingEntries);
    }

    private static string? ExtractFromRefundPaymentHistory(
        IReadOnlyList<CentraRefundPaymentHistory> history,
        Func<string?, string?> extractor)
    {
        foreach (var entry in history)
        {
            var value = NormalizeOptional(extractor(entry.ParamsJson));
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool IsSuccessOrUnknown(string? status)
        => string.IsNullOrWhiteSpace(status) || string.Equals(status.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase);

    private static bool IsRefundEntry(string? entryType)
        => !string.IsNullOrWhiteSpace(entryType) &&
           entryType.Contains("refund", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdyen(string? paymentMethod)
        => !string.IsNullOrWhiteSpace(paymentMethod) &&
           paymentMethod.Contains("adyen", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
