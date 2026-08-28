using System;
using System.Collections.Generic;
using WebApp.Services.ExcelImport;

namespace WebApp.Models.Voucher
{
    public class VoucherImportResult
    {
        public Guid ImportBatchId { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int StagedRows { get; set; }
        public int InvalidRows => Math.Max(0, TotalRows - ValidRows);
        public string? VoucherPostingDate { get; set; }
        public string? VoucherReversalDate { get; set; }
        public List<string> RowHeaders { get; set; } = new();
        public List<ExcelImportRowResult> RowResults { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
