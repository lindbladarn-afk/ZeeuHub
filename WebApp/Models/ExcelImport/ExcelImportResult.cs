using System;
using System.Collections.Generic;

namespace WebApp.Services.ExcelImport
{
    // Shared result model returned by Excel Import handlers and edit-session services.
    public class ExcelImportResult
    {
        public string ImportType { get; set; } = string.Empty;
        public Guid ImportBatchId { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int StagedRows { get; set; }
        public Guid? EditSessionId { get; set; }
        public string? VoucherPostingDate { get; set; }
        public string? VoucherReversalDate { get; set; }
        public List<string> RowHeaders { get; set; } = new();
        public List<ExcelImportRowResult> RowResults { get; set; } = new();
        // Legacy compatibility for callers that still refer to imported rows.
        public int ImportedRows
        {
            get => StagedRows;
            set => StagedRows = value;
        }
        public List<string> Errors { get; set; } = new();

        public static ExcelImportResult Empty(string importType)
        {
            return new ExcelImportResult
            {
                ImportType = importType,
                ImportBatchId = Guid.NewGuid(),
                TotalRows = 0,
                ValidRows = 0,
                InvalidRows = 0,
                StagedRows = 0
            };
        }
    }
}
