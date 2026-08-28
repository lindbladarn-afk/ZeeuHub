using System.Collections.Generic;

namespace WebApp.Models.Voucher
{
    public class VoucherEditRowDto
    {
        public int RowNo { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }
}
