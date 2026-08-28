using WebApp.Models.Integration.Speedrecon;
using WebApp.ViewModels.Shared;

namespace WebApp.ViewModels.Integration.Speedrecon;

// Supplies the Speedrecon operations page with runtime status and execution readiness.
public sealed class SpeedreconPageViewModel
{
    public DateTime ReconDate { get; init; }
    public string? StatusMessage { get; init; }
    public string StatusTone { get; init; } = "info";
    public ModuleBannerViewModel? RuntimeBanner { get; init; }
    public SpeedreconProbeResult Probe { get; init; } = new();

    public bool CanRun =>
        Probe.RuntimeAvailable &&
        Probe.IsEnabledInJeeves &&
        CoreTables.All(TableExists);

    private bool TableExists(string tableName)
        => Probe.Objects.Any(item =>
            string.Equals(item.ObjectName, tableName, StringComparison.OrdinalIgnoreCase) &&
            item.ObjectType == "Table" &&
            item.Exists);

    private static readonly string[] CoreTables =
    [
        "q_zu_speedrecon",
        "q_zu_speedrecon_result"
    ];
}
