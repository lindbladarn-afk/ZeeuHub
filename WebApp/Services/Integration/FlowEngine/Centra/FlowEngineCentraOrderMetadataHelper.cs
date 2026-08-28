using System.Globalization;
using System.Text.Json;
using WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraOrderMetadataHelper
{
    public static string ResolveCustomerReference(FlowEngineCentraOrderStoreConfig config, CentraRawOrder order)
        => config.UseInternalCommentAsCustomerReference
            ? NormalizeOptional(order.InternalComment) ?? string.Empty
            : NormalizeOptional(order.Number) ?? string.Empty;

    public static int? GetShippingMethodId(CentraRawOrder order)
    {
        foreach (var group in order.Attributes)
        {
            foreach (var element in group.Elements)
            {
                if (!string.Equals(element.Key?.Trim(), "converted_method_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var methodId))
                    return methodId;

                return 0;
            }
        }

        return 0;
    }

    public static string? OrderAttributeValue(CentraRawOrder order, string key)
    {
        foreach (var group in order.Attributes)
        {
            foreach (var element in group.Elements)
            {
                if (string.Equals(element.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return NormalizeOptional(element.Value);
            }
        }

        return null;
    }

    public static string? SuccessfulPaymentReference(CentraRawOrder order)
        => FirstNonEmpty(
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status) && IsAuthOrCapture(entry.EntryType), entry => NormalizeOptional(entry.ExternalReference)),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status) && IsAuthOrCapture(entry.EntryType), entry => PaymentReferenceFromParamsJson(entry.ParamsJson)),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status), entry => NormalizeOptional(entry.ExternalReference)),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status), entry => PaymentReferenceFromParamsJson(entry.ParamsJson)),
            ExtractFromEntries(order.PaymentHistory, _ => true, entry => PaymentReferenceFromParamsJson(entry.ParamsJson)));

    public static string? MerchantReferenceFromParamsJson(CentraRawOrder order)
    {
        var merchantReference = FirstNonEmpty(
            ExtractFromSuccessfulAuthEntry(order.PaymentHistory, paramsJson => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(paramsJson, "MerchantReference", "merchantReference")),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status), entry => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(entry.ParamsJson, "MerchantReference", "merchantReference")),
            ExtractFromEntries(order.PaymentHistory, _ => true, entry => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(entry.ParamsJson, "MerchantReference", "merchantReference")));

        if (!string.IsNullOrWhiteSpace(merchantReference))
            return merchantReference;

        return IsAdyen(order.PaymentMethod?.Name) ? SuccessfulPaymentReference(order) : null;
    }

    public static string? PaymentMethodNameFromParamsJson(CentraRawOrder order)
        => FirstNonEmpty(
            ExtractFromSuccessfulAuthEntry(order.PaymentHistory, paramsJson => FlowEngineCentraJsonElementReader.ExtractNestedProperty(paramsJson, "PaymentMethod", "PaymentMethodName")),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status), entry => FlowEngineCentraJsonElementReader.ExtractNestedProperty(entry.ParamsJson, "PaymentMethod", "PaymentMethodName")),
            ExtractFromEntries(order.PaymentHistory, entry => IsSuccess(entry.Status), entry => FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(entry.ParamsJson, "paymentMethod", "PaymentMethod")));

    public static string? InstaboxAvailabilityToken(CentraRawOrder order)
    {
        var raw = OrderAttributeValue(order, "raw_session_response");
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(document.RootElement, "session", out var session) || session.ValueKind != JsonValueKind.Object)
                return null;
            if (!FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(session, "delivery_groups", out var deliveryGroups) || deliveryGroups.ValueKind != JsonValueKind.Array)
                return null;
            var firstGroup = deliveryGroups.EnumerateArray().FirstOrDefault();
            if (firstGroup.ValueKind != JsonValueKind.Object)
                return null;
            if (!FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(firstGroup, "shipping", out var shipping) || shipping.ValueKind != JsonValueKind.Object)
                return null;
            if (!FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(shipping, "meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
                return null;
            return FlowEngineCentraJsonElementReader.ExtractString(meta, "isb.availability_token");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractFromSuccessfulAuthEntry(
        IReadOnlyList<CentraPaymentHistory> history,
        Func<string?, string?> extractor)
        => ExtractFromEntries(history, entry => IsSuccess(entry.Status) && IsAuth(entry.EntryType), entry => extractor(entry.ParamsJson));

    private static string? ExtractFromEntries(
        IReadOnlyList<CentraPaymentHistory> history,
        Func<CentraPaymentHistory, bool> predicate,
        Func<CentraPaymentHistory, string?> extractor)
    {
        foreach (var entry in history)
        {
            if (!predicate(entry))
                continue;

            var value = NormalizeOptional(extractor(entry));
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? PaymentReferenceFromParamsJson(string? paramsJson)
        => FirstNonEmpty(
            FlowEngineCentraJsonElementReader.ExtractFirstArrayObjectPropertyAsString(paramsJson, "PaymentTransactions", "PaymentTransactionId"),
            FlowEngineCentraJsonElementReader.ExtractPropertyWithFallback(paramsJson, "PaymentTransactionId", "paymentTransactionId", "PaymentReference", "paymentReference", "pspReference"));

    private static bool IsSuccess(string? status)
        => string.Equals(status?.Trim(), "SUCCESS", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuth(string? entryType)
        => string.Equals(entryType?.Trim(), "AUTH", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthOrCapture(string? entryType)
        => string.Equals(entryType?.Trim(), "AUTH", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(entryType?.Trim(), "CAPTURE", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdyen(string? paymentMethod)
        => !string.IsNullOrWhiteSpace(paymentMethod) &&
           paymentMethod.Contains("adyen", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
