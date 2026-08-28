using System.Collections.Generic;

namespace WebApp.Models.AI
{
    /// <summary>
    /// Resultat från körd SQL-fråga (read-only).
    /// </summary>
    public sealed class SqlQueryResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public int RowCount { get; set; }

        /// <summary>
        /// True om resultatet trunkerades (t.ex. pga maxRows).
        /// </summary>
        public bool Truncated { get; set; }

        /// <summary>
        /// Kolumnnamn i ordning.
        /// </summary>
        public List<string> Columns { get; } = new();

        /// <summary>
        /// Rader: varje rad innehåller kolumnvärden i samma ordning som Columns.
        /// </summary>
        public List<List<object?>> Rows { get; } = new();

        /// <summary>
        /// SQL som faktiskt kördes (efter ev TOP-injektion).
        /// </summary>
        public string? ExecutedSql { get; set; }
    }
}
