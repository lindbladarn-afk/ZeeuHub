using System;

namespace WebApp.Models.Voucher
{
    public class PortalVoucherStagingRow
    {
        public Guid ImportBatchId { get; set; }
        public int RowNo { get; set; }
        public string Account { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string CurrencyRate { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
        public string Ktonr { get; set; } = string.Empty;
        public string Koststallekod { get; set; } = string.Empty;
        public string Kostbar { get; set; } = string.Empty;
        public string K4 { get; set; } = string.Empty;
        public string K5 { get; set; } = string.Empty;
        public string K6 { get; set; } = string.Empty;
        public string K7 { get; set; } = string.Empty;
        public string Projcode { get; set; } = string.Empty;
        public string Debbel { get; set; } = string.Empty;
        public string Krebel { get; set; } = string.Empty;
        public string Momskod { get; set; } = string.Empty;
        public string VoucherText { get; set; } = string.Empty;
        public string Autoregel { get; set; } = string.Empty;
        public string Valkod { get; set; } = string.Empty;
        public string Rate { get; set; } = string.Empty;
        public string Vbbelopp { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public string ImportedBy { get; set; } = string.Empty;
        public Guid? CompanyId { get; set; }
        public int? ForetagKod { get; set; }
        public string? UserId { get; set; }
        public DateTime? PostingDate { get; set; }
        public DateTime? AterBokfDat { get; set; }
    }
}
