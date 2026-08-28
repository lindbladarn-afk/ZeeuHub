using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraReadSelectionService
{
    internal CentraDateSelection ResolveDateSelection(FlowEngineExecuteJobRequest request)
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
            return new CentraDateSelection(today, today, "latest-day");
        }

        if (dateRaw is not null && (sinceRaw is not null || untilRaw is not null))
            throw new InvalidOperationException("Anvand antingen date eller since/until, inte bada.");

        if (dateRaw is not null)
        {
            var date = ParseDateUtc(dateRaw);
            return new CentraDateSelection(date, date, "date");
        }

        if (sinceRaw is null)
            throw new InvalidOperationException("Centra date maste anges via date eller since.");

        var since = ParseDateUtc(sinceRaw);
        var until = untilRaw is null ? DateTime.UtcNow.Date : ParseDateUtc(untilRaw);
        if (since > until)
            throw new InvalidOperationException("Centra since maste vara samma eller tidigare an until.");

        return new CentraDateSelection(since, until, "range");
    }

    internal List<DateTime> EnumerateDates(DateTime sinceUtc, DateTime untilUtc)
    {
        var dates = new List<DateTime>();
        for (var date = sinceUtc.Date; date <= untilUtc.Date; date = date.AddDays(1))
            dates.Add(date);
        return dates;
    }

    internal string FormatDateUtc(DateTime value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal string NormalizeRequiredValue(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{label} maste anges.");

        return value.Trim();
    }

    private static DateTime ParseDateUtc(string value)
    {
        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidOperationException($"Ogiltigt Centra-datum '{value}'. Forvantat format ar YYYY-MM-DD.");
        }

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }
}
