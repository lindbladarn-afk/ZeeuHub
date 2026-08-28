using System.Globalization;

namespace WebApp.Services.ExcelImport;

// Parses date inputs shared by voucher imports in UI and background jobs.
public static class ExcelImportDateParser
{
    private static readonly string[] SupportedDateFormats =
    {
        "yyyy-MM-dd",
        "yyyyMMdd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff"
    };

    public static bool TryParsePostingDate(string? input, out DateTime postingDate)
    {
        postingDate = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var value = input.Trim();
        return DateTime.TryParseExact(value, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out postingDate)
            || DateTime.TryParse(value, out postingDate);
    }

    public static bool TryParseOptionalDate(string? input, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(input))
            return true;

        if (!TryParsePostingDate(input, out var parsed))
            return false;

        date = parsed.Date;
        return true;
    }
}
