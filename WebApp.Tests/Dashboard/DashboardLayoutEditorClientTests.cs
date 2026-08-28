// Protects the dashboard editor's in-place save, history controls, and pointer-based reordering contracts.
namespace WebApp.Tests;

public sealed class DashboardLayoutEditorClientTests
{
    private static readonly string WebAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));

    [Fact]
    public void Save_Finalizes_The_Local_Grid_And_Loads_Only_New_Cards_In_The_Background()
    {
        var script = ReadLayoutScript();

        Assert.Contains("const finalizeSavedGrid = () =>", script, StringComparison.Ordinal);
        Assert.Contains("shell.replaceChildren(createLazyCardContent(widget.widgetId));", script, StringComparison.Ordinal);
        Assert.Contains("content.dataset.dashboardLazyCard = 'true';", script, StringComparison.Ordinal);
        Assert.Contains("window.ZeeUDashboard?.refreshCards?.();", script, StringComparison.Ordinal);
        Assert.Contains("finishSavedLayout('Startsidan har sparats.');", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch('/Member/DashboardGrid'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_Provides_Visible_Undo_And_Redo_Controls()
    {
        var script = ReadLayoutScript();
        var view = File.ReadAllText(Path.Combine(WebAppRoot, "Views", "Member", "MainDashboard.cshtml"));

        Assert.Contains("data-dashboard-undo", view, StringComparison.Ordinal);
        Assert.Contains("data-dashboard-redo", view, StringComparison.Ordinal);
        Assert.Contains("const undo = () =>", script, StringComparison.Ordinal);
        Assert.Contains("const redo = () =>", script, StringComparison.Ordinal);
        Assert.DoesNotContain("event.ctrlKey || event.metaKey", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+Z", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_Cancels_The_Entire_Editing_Session()
    {
        var script = ReadLayoutScript();

        Assert.Contains("if (event.key !== 'Escape') return;", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();\n            cancel();", script, StringComparison.Ordinal);
        Assert.DoesNotContain("event.key === 'Escape' && pointerDrag", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Reordering_Uses_Pointer_Events_And_A_Visible_Placeholder()
    {
        var script = ReadLayoutScript();
        var stylesheet = File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "css", "dashboard.css"));

        Assert.Contains("document.addEventListener('pointerdown', handlePointerDown);", script, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('pointermove', handlePointerMove", script, StringComparison.Ordinal);
        Assert.Contains("dashboard-widget-placeholder", script, StringComparison.Ordinal);
        Assert.Contains(".dashboard-widget-placeholder", stylesheet, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_Only_Offers_Sizes_Supported_By_Each_Card()
    {
        var script = ReadLayoutScript();
        var view = File.ReadAllText(Path.Combine(WebAppRoot, "Views", "Member", "MainDashboard.cshtml"));
        var grid = File.ReadAllText(Path.Combine(WebAppRoot, "Views", "Member", "Dashboard", "_DashboardGrid.cshtml"));

        Assert.Contains("data-dashboard-supported-sizes", view, StringComparison.Ordinal);
        Assert.Contains("getSupportedSizes(widget.widgetId).forEach", script, StringComparison.Ordinal);
        Assert.Contains("getAllowedSize(widgetId, size)", script, StringComparison.Ordinal);
        Assert.Contains("card.SupportedSizes", grid, StringComparison.Ordinal);
    }

    private static string ReadLayoutScript()
        => File.ReadAllText(Path.Combine(WebAppRoot, "wwwroot", "js", "dashboard", "dashboard-layout.js"));
}
