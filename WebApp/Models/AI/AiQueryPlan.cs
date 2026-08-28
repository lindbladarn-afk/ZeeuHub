// Defines the structured, user-independent plan used before Intelligence executes SQL.
using System.Text.Json.Serialization;

namespace WebApp.Models.AI;

public sealed class AiQueryPlan
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "lookup";

    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("dimensions")]
    public List<string> Dimensions { get; set; } = [];

    [JsonPropertyName("filters")]
    public List<AiQueryPlanFilter> Filters { get; set; } = [];

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("comparison")]
    public string? Comparison { get; set; }

    [JsonPropertyName("time_grain")]
    public string? TimeGrain { get; set; }

    [JsonPropertyName("result_contract")]
    public AiQueryResultContract ResultContract { get; set; } = new();

    [JsonPropertyName("sort")]
    public string? Sort { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];
}

public sealed class AiQueryResultContract
{
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "table";

    [JsonPropertyName("required_roles")]
    public List<string> RequiredRoles { get; set; } = [];

    [JsonPropertyName("preferred_visualization")]
    public string? PreferredVisualization { get; set; }
}

public sealed class AiQueryPlanFilter
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public sealed class AiQueryEvidence
{
    public string VerificationStatus { get; set; } = "not_applicable";
    public string? DataSource { get; set; }
    public string? MetricLabel { get; set; }
    public string? Period { get; set; }
    public List<string> Dimensions { get; set; } = [];
    public List<string> SourceTables { get; set; } = [];
    public List<string> Facts { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}
