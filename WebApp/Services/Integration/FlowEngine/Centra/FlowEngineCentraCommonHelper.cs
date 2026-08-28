using System.Text.Json;
using OrdersContracts = WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;
using ReturnsContracts = WebApp.Services.Integration.FlowEngine.CentraSendReturnsContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraCommonHelper
{
    public static DateTime ResolveTargetDateUtc(string? dateUtc, string operationLabel)
    {
        if (string.IsNullOrWhiteSpace(dateUtc))
            return DateTime.UtcNow.Date;

        if (!DateTime.TryParse(dateUtc, out var parsed))
            throw new InvalidOperationException($"Datum maste anges i formatet yyyy-MM-dd for Centra {operationLabel}.");

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    public static bool TryGetGraphQlErrorMessage(string body, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return false;

            var messages = errors.EnumerateArray()
                .Select(item => item.TryGetProperty("message", out var message) ? message.GetString() : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            if (messages.Count == 0)
                return false;

            errorMessage = string.Join(" | ", messages!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? ResolveDeliveryName(bool useCompanyNameForAddress, OrdersContracts.CentraShippingAddress? address)
    {
        if (useCompanyNameForAddress && !string.IsNullOrWhiteSpace(address?.CompanyName))
            return address.CompanyName;

        return BuildDeliveryName(address?.FirstName, address?.LastName);
    }

    public static string? ResolveDeliveryName(FlowEngineCentraOrderStoreConfig config, OrdersContracts.CentraRawOrder order)
        => ResolveDeliveryName(config.UseCompanyNameForAddress, order.ShippingAddress);

    public static string? ResolveDeliveryName(FlowEngineCentraReturnStoreConfig config, ReturnsContracts.CentraShippingAddress? address)
    {
        if (config.UseCompanyNameForAddress && !string.IsNullOrWhiteSpace(address?.CompanyName))
            return address.CompanyName;

        return BuildDeliveryName(address?.FirstName, address?.LastName);
    }

    private static string? BuildDeliveryName(string? firstName, string? lastName)
    {
        var parts = new[] { NormalizeOptional(firstName), NormalizeOptional(lastName) }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var name = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
