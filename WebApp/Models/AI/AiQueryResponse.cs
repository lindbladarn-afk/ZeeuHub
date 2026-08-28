using System.Text.Json.Serialization;

namespace WebApp.Models.AI
{
    /// <summary>
    /// Represents the user-facing result of an Intelligence query.
    /// </summary>
    public sealed class AiQueryResponse
    {
        /// <summary>Stable identifier used when the user submits answer feedback.</summary>
        public Guid ResponseId { get; set; } = Guid.NewGuid();

        /// <summary>True om frågan kunde hanteras och leverera ett användbart svar.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Slutgiltigt svar som visas i chatten</summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>SQL som faktiskt kördes</summary>
        public string? Sql { get; set; }

        /// <summary>Ev varning, t.ex. om frågan blockerades</summary>
        public string? Warning { get; set; }

        /// <summary>Teknisk feltext endast för serverloggning och felsökning.</summary>
        [JsonIgnore]
        public string? ErrorMessage { get; set; }

        /// <summary>Strukturerat och säkert fel som kan visas för användaren.</summary>
        public AiQueryError? Error { get; set; }

        /// <summary>Kontextuella följdfrågor som användaren kan välja direkt.</summary>
        public List<string> Suggestions { get; set; } = new();

        /// <summary>Den validerade analysplan som låg till grund för resultatet.</summary>
        public AiQueryPlan? Plan { get; set; }

        /// <summary>Spårbar information om datakälla, mått och resultatverifiering.</summary>
        public AiQueryEvidence? Evidence { get; set; }

        /// <summary>Serverdiagnostik som loggas men aldrig skickas till webbläsaren.</summary>
        [JsonIgnore]
        public AiExecutionDiagnostics Diagnostics { get; set; } = new();

        /// <summary>Kolumnnamn från SQL-resultatet</summary>
        public List<string>? Columns { get; set; }

        /// <summary>Rader (samma ordning som Columns)</summary>
        public List<List<object?>>? Rows { get; set; }

        /// <summary>Antal rader som hämtades</summary>
        public int? RowCount { get; set; }

        /// <summary>True om resultatet trunkerades</summary>
        public bool? Truncated { get; set; }

        /// <summary>Antal prompt-tokens som förbrukades för frågan</summary>
        public int? PromptTokens { get; set; }

        /// <summary>Antal completion-tokens som förbrukades för frågan</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>Totalt antal tokens som förbrukades för frågan</summary>
        public int? TotalTokens { get; set; }

        /// <summary>Kostnad för prompt-tokens (SEK, standardpris)</summary>
        public decimal? InputCostSek { get; set; }

        /// <summary>Kostnad för completion-tokens (SEK, standardpris)</summary>
        public decimal? OutputCostSek { get; set; }

        /// <summary>Total kostnad (SEK, standardpris)</summary>
        public decimal? TotalCostSek { get; set; }

        /// <summary>Quota-status för klientlogik (allowed/warning/needs_decision/blocked/paid/disabled).</summary>
        public string? QuotaStatus { get; set; }

        /// <summary>Användarvänligt quota-meddelande.</summary>
        public string? QuotaMessage { get; set; }

        /// <summary>Förbrukade tokens i aktuell period.</summary>
        public int? QuotaUsedTokens { get; set; }

        /// <summary>Gratis tokens i aktuell period.</summary>
        public int? QuotaFreeTokens { get; set; }

        /// <summary>Förbrukning i procent.</summary>
        public int? QuotaUsagePercent { get; set; }

        /// <summary>Total kostnad (SEK) för aktuell period.</summary>
        public decimal? QuotaPeriodTotalCostSek { get; set; }

        /// <summary>Tokens utöver fri periodgräns (debiterbara).</summary>
        public int? QuotaPaidExtraTokens { get; set; }

        /// <summary>Kostnad (SEK) för debiterbara extra tokens i aktuell period.</summary>
        public decimal? QuotaPaidExtraCostSek { get; set; }

        /// <summary>Om användaren måste välja paid/block innan nästa fråga.</summary>
        public bool QuotaNeedsDecision { get; set; }

        /// <summary>Om användaren kör i betalläge efter slut på fria tokens.</summary>
        public bool QuotaPaidMode { get; set; }
    }

    public sealed class AiQueryError
    {
        public string Code { get; set; } = "unexpected";
        public string Title { get; set; } = "Frågan kunde inte slutföras";
        public string Message { get; set; } = "Försök igen om en stund.";
        public bool CanRetry { get; set; }
        public string Tone { get; set; } = "danger";
    }
}
