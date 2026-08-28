namespace WebApp.ViewModels.Shared;

// Shared view model for empty, loading and warning states across portal modules.
// Keeps copy, icon and optional action in one shape so views can reuse the same partial instead of inlining one-off cards.
public sealed class ModuleStateViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Note { get; set; }
    public string Tone { get; set; } = "neutral";
    public string IconClass { get; set; } = "fa fa-circle-info";
    public bool Compact { get; set; }
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
}
