using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifySelectionService : IFlowEngineShopifySelectionService
{
    private const int DefaultAccessibleHistoryDays = 60;

    public FlowEngineShopifyDateSelection ResolveDateSelection(FlowEngineExecuteJobRequest request)
    {
        var dateRaw = string.IsNullOrWhiteSpace(request.Params.DateUtc) ? null : request.Params.DateUtc.Trim();
        var sinceRaw = string.IsNullOrWhiteSpace(request.Params.SinceUtc) ? null : request.Params.SinceUtc.Trim();
        var untilRaw = string.IsNullOrWhiteSpace(request.Params.UntilUtc) ? null : request.Params.UntilUtc.Trim();
        var useLatestDay = request.Params.UseLatestDay;

        if (useLatestDay && (dateRaw is not null || sinceRaw is not null || untilRaw is not null))
            throw new InvalidOperationException("Anvand antingen latest-day eller date/since/until, inte bada.");

        if (useLatestDay)
        {
            var today = DateTime.UtcNow.Date;
            return new FlowEngineShopifyDateSelection(today, today, "latest-day");
        }

        if (dateRaw is not null && (sinceRaw is not null || untilRaw is not null))
            throw new InvalidOperationException("Anvand antingen date eller since/until, inte bada.");

        if (dateRaw is not null)
        {
            var date = ParseDateUtc(dateRaw);
            return new FlowEngineShopifyDateSelection(date, date, "date");
        }

        if (sinceRaw is null)
            throw new InvalidOperationException("Shopify date maste anges via date eller since.");

        var since = ParseDateUtc(sinceRaw);
        var until = untilRaw is null ? DateTime.UtcNow.Date : ParseDateUtc(untilRaw);
        if (since > until)
            throw new InvalidOperationException("Shopify since maste vara samma eller tidigare an until.");

        return new FlowEngineShopifyDateSelection(since, until, "range");
    }

    public string FormatDateUtc(DateTime date)
        => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public List<DateTime> EnumerateDates(DateTime sinceUtc, DateTime untilUtc)
    {
        var dates = new List<DateTime>();
        for (var date = sinceUtc.Date; date <= untilUtc.Date; date = date.AddDays(1))
            dates.Add(date);
        return dates;
    }

    public bool RequiresReadAllOrders(DateTime earliestDateUtc)
        => earliestDateUtc.Date < DateTime.UtcNow.Date.AddDays(-DefaultAccessibleHistoryDays);

    public string BuildDateSearchQuery(DateTime dateUtc)
    {
        var start = dateUtc.Date.ToString("yyyy-MM-dd'T'00:00:00'Z'", CultureInfo.InvariantCulture);
        var end = dateUtc.Date.ToString("yyyy-MM-dd'T'23:59:59'Z'", CultureInfo.InvariantCulture);
        return $"status:any created_at:>={start} created_at:<={end}";
    }

    public string? ParseUpdatedSince(string? rawValue)
    {
        var trimmed = Normalize(rawValue);
        if (trimmed is null)
            return null;

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedOffset))
            return parsedOffset.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
            return DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        throw new InvalidOperationException($"Ogiltigt Shopify updated-since '{trimmed}'. Anvand ISO-8601 eller YYYY-MM-DD.");
    }

    public string? BuildProductsSearchQuery(string? baseQuery, string? updatedSince)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(updatedSince))
            parts.Add($"updated_at:>={updatedSince}");

        var query = Normalize(baseQuery);
        if (query is not null)
            parts.Add(query);

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    public string BuildSelectionSummaryLabel(string selectionKind, string? date, string? sinceUtc, string? untilUtc)
    {
        return selectionKind switch
        {
            "latest-day" => "latest-day",
            "range" => $"{sinceUtc} -> {untilUtc}",
            _ => date ?? sinceUtc ?? string.Empty
        };
    }

    public string NormalizeOrderGid(string? rawOrderId)
    {
        if (string.IsNullOrWhiteSpace(rawOrderId))
            throw new InvalidOperationException("Shopify order-id maste anges.");

        var trimmed = rawOrderId.Trim();
        if (trimmed.StartsWith("gid://shopify/Order/", StringComparison.OrdinalIgnoreCase))
        {
            var numericId = ExtractNumericIdFromGid(trimmed);
            if (string.IsNullOrWhiteSpace(numericId))
                throw new InvalidOperationException($"Ogiltigt Shopify order-id '{trimmed}'.");

            return $"gid://shopify/Order/{numericId}";
        }

        if (trimmed.All(char.IsDigit))
            return $"gid://shopify/Order/{trimmed}";

        throw new InvalidOperationException($"Ogiltigt Shopify order-id '{trimmed}'. Anvand gid://shopify/Order/<id> eller ett numeriskt id.");
    }

    public string? ExtractNumericIdFromGid(string? orderGid)
    {
        if (string.IsNullOrWhiteSpace(orderGid))
            return null;

        var last = orderGid.Trim().Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(last) || !last.All(char.IsDigit) ? null : last;
    }

    private static DateTime ParseDateUtc(string? dateUtc)
    {
        var raw = string.IsNullOrWhiteSpace(dateUtc)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dateUtc.Trim();

        if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            throw new InvalidOperationException($"Ogiltigt Shopify-datum '{raw}'. Forvantat format ar YYYY-MM-DD.");

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
