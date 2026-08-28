using System.Collections.Generic;

namespace WebApp.Services.ExcelImport
{
    // Shared row-level validation result for Excel Import previews and edit sessions.
    public class ExcelImportRowResult
    {
        public int RowNo { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
