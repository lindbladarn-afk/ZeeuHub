using System.Collections.Generic;

namespace WebApp.Models.Budget
{
    public class BudgetEditRowDto
    {
        public int RowNo { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }
}
