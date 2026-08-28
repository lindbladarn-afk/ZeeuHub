namespace WebApp.ViewModels.Shared;

public sealed class ModuleUnavailableViewModel
{
    public string ModuleLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public ModuleStateViewModel State { get; set; } = new();
}
