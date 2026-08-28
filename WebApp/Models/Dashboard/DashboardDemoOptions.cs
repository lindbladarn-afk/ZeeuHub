// Configures the internal demo gate for the information-driven dashboard layout.
namespace WebApp.Models.Dashboard;

public sealed class DashboardDemoOptions
{
    public const string SectionName = "DashboardDemo";

    public bool Enabled { get; set; } = true;
    public string AllowedCompanyName { get; set; } = "ZeeU AB";
}
