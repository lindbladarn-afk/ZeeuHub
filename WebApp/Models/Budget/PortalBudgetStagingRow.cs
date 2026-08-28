using System;

namespace WebApp.Models.Budget
{
    public class PortalBudgetStagingRow
    {
        public Guid ImportBatchId { get; set; }
        public int RowNo { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; }
        public string ImportedBy { get; set; } = string.Empty;
        public Guid? CompanyId { get; set; }
        public int? ForetagKod { get; set; }
        public string? UserId { get; set; }
    }
}
