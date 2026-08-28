namespace WebApp.Models.Integration;

public sealed class FlowEngineSchedulerPopoverViewModel
{
    public string ToggleKey { get; set; } = string.Empty;
    public IReadOnlyList<(string Label, string Value)> InfoRows { get; set; } = Array.Empty<(string Label, string Value)>();
    public IReadOnlyList<string> NextRuns { get; set; } = Array.Empty<string>();
}
