namespace WebApp.ViewModels.Shared;

// Shared detail item for richer module result cards.
// Useful when a result needs structured rows such as import errors, validation issues or matched records.
public sealed class ModuleResultItemViewModel
{
    public string Summary { get; set; } = string.Empty;
    public string? DetailLabel { get; set; }
    public string? Detail { get; set; }
    public int? RowNumber { get; set; }
}
