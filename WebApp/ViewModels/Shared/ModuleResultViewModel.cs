namespace WebApp.ViewModels.Shared;

// Shared rich result card model for module flows that need more than a simple banner.
// Supports dismiss, dynamic tone changes and optional detail rows so imports and validation-heavy modules can reuse one pattern.
public sealed class ModuleResultViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Note { get; set; }
    public string Tone { get; set; } = "info";
    public string IconClass { get; set; } = "fa fa-info-circle";
    public string? HtmlId { get; set; }
    public bool Dismissible { get; set; }
    public bool AllowUpdate { get; set; }
    public IReadOnlyList<ModuleResultItemViewModel> Items { get; set; } = Array.Empty<ModuleResultItemViewModel>();
}
