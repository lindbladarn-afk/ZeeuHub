namespace WebApp.Models.Integration;

public sealed class FlowEngineTechnicalDetailsViewModel
{
    public string SummaryTitle { get; set; } = "Technical details";
    public string CopyTargetId { get; set; } = string.Empty;
    public string RawDetailsText { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string? StandardError { get; set; }
    public bool ShowEmptyStandardError { get; set; }
    public bool ShowStandardErrorInfoNote { get; set; }
    public string EmptyStandardErrorText { get; set; } = "No STDERR output captured.";
}
