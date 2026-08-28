namespace WebApp.Services.NotifyMe;

public static class NotifyMeScheduleCalculator
{
    public static DateTime CalculateNextExecution(
        DateTime referenceUtc,
        DateTime? startDate,
        string? schemaCode,
        string? scheduleCode)
    {
        var normalizedReferenceUtc = referenceUtc.Kind == DateTimeKind.Utc
            ? referenceUtc
            : referenceUtc.ToUniversalTime();
        var referenceLocal = NotifyMeTimeZoneHelper.ToStockholmTime(normalizedReferenceUtc);
        var startLocal = NotifyMeTimeZoneHelper.NormalizeLocalScheduleAnchor(startDate, referenceLocal);
        var earliestLocal = startLocal > referenceLocal ? startLocal : referenceLocal;

        var nextLocal = scheduleCode switch
        {
            "20" => CalculateWeekly(earliestLocal, startLocal, schemaCode),
            "30" => CalculateMonthly(earliestLocal, startLocal, schemaCode),
            _ => CalculateDaily(earliestLocal, startLocal, schemaCode)
        };

        return NotifyMeTimeZoneHelper.ToUtcFromStockholmLocal(nextLocal);
    }

    private static DateTime CalculateDaily(DateTime earliestLocal, DateTime startLocal, string? schemaCode)
    {
        if (schemaCode == "40")
            return earliestLocal.AddHours(1);

        var daySlot = WithTimeOfDay(earliestLocal.Date, startLocal, 8, 0);
        var nightSlot = WithTimeOfDay(earliestLocal.Date, startLocal, 18, 0);

        return schemaCode switch
        {
            "20" => nightSlot > earliestLocal ? nightSlot : nightSlot.AddDays(1),
            "30" => NextOf(earliestLocal, daySlot, nightSlot, daySlot.AddDays(1)),
            _ => daySlot > earliestLocal ? daySlot : daySlot.AddDays(1)
        };
    }

    private static DateTime CalculateWeekly(DateTime earliestLocal, DateTime startLocal, string? schemaCode)
    {
        var nextWeekAnchor = earliestLocal.Date.AddDays(7);
        return CalculateDaily(nextWeekAnchor, startLocal, schemaCode);
    }

    private static DateTime CalculateMonthly(DateTime earliestLocal, DateTime startLocal, string? schemaCode)
    {
        var nextMonthDate = earliestLocal.Date.AddMonths(1);
        return CalculateDaily(nextMonthDate, startLocal, schemaCode);
    }

    private static DateTime WithTimeOfDay(DateTime day, DateTime startLocal, int fallbackHour, int fallbackMinute)
    {
        var hour = startLocal.TimeOfDay == TimeSpan.Zero ? fallbackHour : startLocal.Hour;
        var minute = startLocal.TimeOfDay == TimeSpan.Zero ? fallbackMinute : startLocal.Minute;
        return new DateTime(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime NextOf(DateTime earliestLocal, params DateTime[] candidates)
    {
        return candidates
            .Where(x => x > earliestLocal)
            .OrderBy(x => x)
            .FirstOrDefault(earliestLocal.AddHours(12));
    }
}
