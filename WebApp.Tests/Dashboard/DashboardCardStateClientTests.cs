// Protects the dashboard card state, retry, and freshness rendering contracts.
namespace WebApp.Tests;

public sealed class DashboardCardStateClientTests
{
    private static readonly string WebAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));

    [Fact]
    public void Card_Content_Uses_One_Shared_State_And_Freshness_Shell()
    {
        var contentView = ReadView("_DashboardCardContent.cshtml");
        var stateView = ReadView("_DashboardCardState.cshtml");

        Assert.Contains("Dashboard/_DashboardCardState", contentView, StringComparison.Ordinal);
        Assert.Contains("Dashboard/_DashboardCardFreshness", contentView, StringComparison.Ordinal);
        Assert.Contains("data-dashboard-card-refresh-url", contentView, StringComparison.Ordinal);
        Assert.Contains("data-dashboard-card-retry", stateView, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_Retries_A_Card_Without_Reloading_The_Page()
    {
        var script = File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "js", "dashboard.js"));

        Assert.Contains("const loadDashboardCard = (shell, url) =>", script, StringComparison.Ordinal);
        Assert.Contains("shell.replaceWith(nextNode);", script, StringComparison.Ordinal);
        Assert.Contains("data-dashboard-card-retry", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_Formats_The_Update_Time_In_The_Users_Locale()
    {
        var script = File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "js", "dashboard.js"));

        Assert.Contains("new Intl.DateTimeFormat('sv-SE'", script, StringComparison.Ordinal);
        Assert.Contains("data-dashboard-updated-at", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Ai_Response_Is_Escaped_Before_It_Is_Added_To_The_Page()
    {
        var script = File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "js", "dashboard.js"));

        Assert.Contains("const escapeHtml = value =>", script, StringComparison.Ordinal);
        Assert.Contains("`<p>${escapeHtml(line)}</p>`", script, StringComparison.Ordinal);
        Assert.Contains("error.textContent = msg;", script, StringComparison.Ordinal);
        Assert.DoesNotContain("`<p>${line}</p>`", script, StringComparison.Ordinal);
    }

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.Combine(WebAppRoot, "Views", "Member", "Dashboard", fileName));
}
