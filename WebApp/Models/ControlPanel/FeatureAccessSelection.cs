namespace WebApp.Models.ControlPanel;

public class FeatureAccessSelection
{
    public int CompanyCode { get; set; }
    public bool InvoicesEnabled { get; set; } = true;
    public bool OrdersEnabled { get; set; } = true;
    public bool AiEnabled { get; set; } = true;
    public bool ExcelImportEnabled { get; set; } = true;
    public bool DashboardEnabled { get; set; } = true;
}
