// Carries server-only timing and retry diagnostics through the Intelligence pipeline.
using System.Text.Json.Serialization;

namespace WebApp.Models.AI;

public sealed class AiExecutionDiagnostics
{
    public const string CurrentPromptVersion = "intelligence-plan-v2";

    public string PromptVersion { get; set; } = CurrentPromptVersion;
    public string? ModelDeployment { get; set; }
    public int ModelRetryCount { get; set; }
    public long PlanningDurationMs { get; set; }
    public long SqlDurationMs { get; set; }
    public long SummaryDurationMs { get; set; }
    public long TotalDurationMs { get; set; }

    [JsonIgnore]
    public string? ErrorCode { get; set; }
}
