using System.Collections.Generic;

namespace WebApp.Models.PriceUpdate
{
    public class PriceUpdateEditRowDto
    {
        public int RowNo { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }
}
