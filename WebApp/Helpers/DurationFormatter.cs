namespace WebApp.Helpers;

public static class DurationFormatter
{
    public static string ToFriendlyTime(int minutes)
    {
        if (minutes <= 0) return "0 min";
        if (minutes < 60) return $"{minutes} min";

        var total = minutes;
        var days = total / 1440;
        total %= 1440;
        var hours = total / 60;
        var mins = total % 60;

        if (days > 0)
        {
            return mins == 0
                ? $"{days} d {hours} h"
                : $"{days} d {hours} h {mins} min";
        }

        return mins == 0 ? $"{hours} h" : $"{hours} h {mins} min";
    }
}
