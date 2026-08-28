using Entities.User;

namespace WebApp.Models.ControlPanel;

public class FeatureAccessItem
{
    public int CompanyCode { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool InvoicesEnabled { get; set; }
    public bool OrdersEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool ExcelImportEnabled { get; set; }
    public bool DashboardEnabled { get; set; }
}
