using System.Text.Json.Serialization;

namespace WebApp.Models.AI
{
    /// <summary>
    /// Tar emot användarens AI-fråga och serverstyrda körkontext.
    /// </summary>
    public sealed class AiQueryRequest
    {
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// Vart frågan kommer ifrån (används för routing/guardrails).
        /// Rekommenderade värden: "intelligence" | "dashboard".
        /// </summary>
        public string? Source { get; set; }

        /// <summary>Vilken datasource som UI valt (t.ex. "tenant").</summary>
        public string? DataSourceKey { get; set; }

        /// <summary>Om ni vill kunna styra "jeeves" vs "fabric" via request (valfritt)</summary>
        public string? Provider { get; set; } // "jeeves" | "fabric"

        /// <summary>Verifierad bolagskod från serverns runtime-kontext.</summary>
        [JsonIgnore]
        public int? CompanyCode { get; set; }

        /// <summary>Verifierad tenant-anslutning från serverns runtime-kontext.</summary>
        [JsonIgnore]
        public string? RuntimeConnectionString { get; set; }
    }
}
