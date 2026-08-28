namespace WebApp.Models.AI;

/// <summary>
/// Carries user decision when free AI quota is exhausted.
/// This model controls whether usage should continue in paid mode or remain blocked until reset.
/// </summary>
public sealed class AiQuotaDecisionRequest
{
    public string Choice { get; set; } = string.Empty; // allow_paid | block_until_reset
}

