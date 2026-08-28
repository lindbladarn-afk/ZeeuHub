using System;
using System.Collections.Generic;

namespace WebApp.Services.Application.AI.Quota;

/// <summary>
/// Configures AI quota behavior.
/// This class controls free-token allowance, warning threshold, and optional
/// per-company overrides, while allowing the entire feature to be toggled on/off.
/// </summary>
public sealed class AiQuotaOptions
{
    public bool Enabled { get; set; } = false;
    public int FreeTokensPerPeriod { get; set; } = 50_000;
    public int WarningThresholdPercent { get; set; } = 75;
    public List<AiQuotaCompanyOverride> CompanyOverrides { get; set; } = new();
}

public sealed class AiQuotaCompanyOverride
{
    public Guid CompanyId { get; set; }
    public int? FreeTokensPerPeriod { get; set; }
    public int? WarningThresholdPercent { get; set; }
}
