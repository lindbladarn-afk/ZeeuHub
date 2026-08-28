using WebApp.Models.Integration.Speedrecon;

namespace WebApp.Services.Integration.Speedrecon;

// Describes how one Speedrecon SQL module maps into hub orchestration.
public sealed class SpeedreconModuleDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required Func<SpeedreconRunPlan, bool> IsEnabled { get; init; }
    public required IReadOnlyList<string> ResultDescriptions { get; init; }
}
