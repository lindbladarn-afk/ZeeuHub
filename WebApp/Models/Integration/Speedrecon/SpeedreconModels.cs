namespace WebApp.Models.Integration.Speedrecon;

// Models the Speedrecon runtime state read from the active Jeeves company.
public sealed class SpeedreconSchemaColumn
{
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public int ColumnId { get; init; }
    public string ColumnName { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public short MaxLength { get; init; }
    public byte Precision { get; init; }
    public byte Scale { get; init; }
    public bool IsNullable { get; init; }
    public string? DefaultDefinition { get; init; }
}

public sealed class SpeedreconObjectStatus
{
    public string ObjectName { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public bool Exists { get; init; }
}

public sealed class SpeedreconPlanRow
{
    public DateTime ReconDate { get; init; }
    public DateTime? ExecDate { get; init; }
    public string PersSign { get; init; } = string.Empty;
    public int EnabledChecks { get; init; }
    public bool IsLocked { get; init; }
}

public sealed class SpeedreconRunPlan
{
    public DateTime ExecDate { get; init; }
    public DateTime ReconDate { get; init; }
    public bool Kundreskontra { get; init; }
    public bool Leverantorsreskontra { get; init; }
    public bool Anlaggning { get; init; }
    public bool InlevereratEjFakturerat { get; init; }
    public bool InternLeverantorsreskontra { get; init; }
    public bool Lagervarde { get; init; }
    public bool Lagerflytt { get; init; }
    public bool Orderunik { get; init; }
    public bool Periodisering { get; init; }
    public bool Pia { get; init; }
    public bool UtlevereratEjFakturerat { get; init; }
}

public sealed class SpeedreconResultSummaryRow
{
    public DateTime ReconDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public decimal ReconAmount { get; init; }
    public decimal GlAmount { get; init; }
    public decimal Difference { get; init; }
    public int DifferenceRows { get; init; }
}

public sealed class SpeedreconRunRequest
{
    public DateTime ReconDate { get; init; }
}

public sealed class SpeedreconProbeResult
{
    public string CompanyName { get; init; } = string.Empty;
    public int CompanyCode { get; init; }
    public string? PersSign { get; init; }
    public bool RuntimeAvailable { get; init; }
    public string? RuntimeMessage { get; init; }
    public bool IsEnabledInJeeves { get; init; }
    public DateTime ProbeTimeUtc { get; init; }
    public IReadOnlyList<SpeedreconObjectStatus> Objects { get; init; } = Array.Empty<SpeedreconObjectStatus>();
    public IReadOnlyList<SpeedreconSchemaColumn> Columns { get; init; } = Array.Empty<SpeedreconSchemaColumn>();
    public IReadOnlyList<SpeedreconPlanRow> PlanRows { get; init; } = Array.Empty<SpeedreconPlanRow>();
    public IReadOnlyList<SpeedreconResultSummaryRow> ResultSummary { get; init; } = Array.Empty<SpeedreconResultSummaryRow>();
}

public sealed class SpeedreconRunOutcome
{
    public int PlanCount { get; init; }
    public int ModuleCount { get; init; }
    public DateTime ReconDate { get; init; }
    public IReadOnlyList<string> ModuleNames { get; init; } = Array.Empty<string>();
}
