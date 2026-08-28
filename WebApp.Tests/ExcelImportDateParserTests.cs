using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies shared Excel Import date parsing for UI and background jobs.
public sealed class ExcelImportDateParserTests
{
    [Theory]
    [InlineData("2026-04-27")]
    [InlineData("20260427")]
    [InlineData("2026-04-27T10:15:30")]
    public void TryParsePostingDate_Accepts_Supported_Date_Formats(string input)
    {
        var parsed = ExcelImportDateParser.TryParsePostingDate(input, out var date);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 4, 27), date.Date);
    }

    [Fact]
    public void TryParseOptionalDate_Allows_Empty_Value()
    {
        var parsed = ExcelImportDateParser.TryParseOptionalDate(string.Empty, out var date);

        Assert.True(parsed);
        Assert.Null(date);
    }
}
