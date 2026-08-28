namespace WebApp.Services.NotifyMe;

public static class NotifyMeTimeZoneHelper
{
    private static readonly TimeZoneInfo StockholmTimeZone = ResolveStockholmTimeZone();

    public static DateTime StockholmNow => ToStockholmTime(DateTime.UtcNow);

    public static DateTime ToStockholmTime(DateTime value)
    {
        var timestamp = value;
        if (timestamp.Kind == DateTimeKind.Unspecified)
            timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

        if (timestamp.Kind == DateTimeKind.Local)
            return TimeZoneInfo.ConvertTime(timestamp, StockholmTimeZone);

        return TimeZoneInfo.ConvertTimeFromUtc(timestamp, StockholmTimeZone);
    }

    public static DateTime? ToStockholmTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return ToStockholmTime(value.Value);
    }

    public static DateTime ToUtcFromStockholmLocal(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeToUtc(value, StockholmTimeZone);
    }

    public static DateTime NormalizeLocalScheduleAnchor(DateTime? startDate, DateTime stockholmReference)
    {
        if (!startDate.HasValue)
            return stockholmReference;

        var anchor = startDate.Value;
        if (anchor.Kind == DateTimeKind.Utc)
            return ToStockholmTime(anchor);

        if (anchor.Kind == DateTimeKind.Local)
            return TimeZoneInfo.ConvertTime(anchor, StockholmTimeZone);

        return DateTime.SpecifyKind(anchor, DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo ResolveStockholmTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
