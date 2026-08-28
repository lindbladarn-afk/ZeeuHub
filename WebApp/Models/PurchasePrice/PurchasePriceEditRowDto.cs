using System.Collections.Generic;

namespace WebApp.Models.PurchasePrice
{
    public class PurchasePriceEditRowDto
    {
        public int RowNo { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }
}
