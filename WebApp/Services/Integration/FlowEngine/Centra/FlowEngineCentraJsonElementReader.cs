using System.Text.Json;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraJsonElementReader
{
    public static string? ExtractPropertyWithFallback(string? raw, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var key in keys)
            {
                if (TryGetPropertyCaseInsensitive(document.RootElement, key, out var value))
                {
                    var stringValue = ElementToString(value);
                    if (!string.IsNullOrWhiteSpace(stringValue))
                        return stringValue;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static string? ExtractNestedProperty(string? raw, params string[] path)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            var current = document.RootElement;
            foreach (var segment in path)
            {
                if (!TryGetPropertyCaseInsensitive(current, segment, out var next))
                    return null;
                current = next;
            }

            return ElementToString(current);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ExtractFirstArrayObjectPropertyAsString(string? raw, string arrayKey, string propertyKey)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!TryGetPropertyCaseInsensitive(document.RootElement, arrayKey, out var array) || array.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                if (!TryGetPropertyCaseInsensitive(item, propertyKey, out var property))
                    continue;
                var extracted = ElementToString(property);
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public static string? ExtractString(JsonElement element, string propertyName)
        => TryGetPropertyCaseInsensitive(element, propertyName, out var value)
            ? ElementToString(value)
            : null;

    public static string? ElementToString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
}
