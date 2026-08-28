namespace WebApp.ViewModels.Shared;

// Shared banner model for informational, success and warning messages in portal modules.
// Keeps copy and tone consistent so views avoid one-off Bootstrap alert variants.
public sealed class ModuleBannerViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Note { get; set; }
    public string Tone { get; set; } = "info";
    public string IconClass { get; set; } = "fa fa-circle-info";
    public bool Compact { get; set; }
}
