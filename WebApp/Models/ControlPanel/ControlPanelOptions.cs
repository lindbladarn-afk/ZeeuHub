namespace WebApp.Models.ControlPanel;

// Holds Control Panel access rules that should come from configuration.
public sealed class ControlPanelOptions
{
    public const string SectionName = "ControlPanel";

    public string AllowedCompanyName { get; set; } = string.Empty;
}
