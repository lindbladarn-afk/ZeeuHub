// Defines the internal demo data available to dashboard card providers.
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Models.Dashboard;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Orders;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.Dashboard.Demo;

public interface IDashboardDemoDataService
{
    bool ShouldUseDemoData(UserSession? user);
    NotifyMeOverviewVm BuildNotifyMeOverview();
    OrderDeliveryForecastViewModel BuildDeliveryForecast();
    ActionCenterViewModel BuildActionCenter();
    InventoryStatusCardViewModel BuildInventoryStatus();
    PurchaseAcknowledgementCardViewModel BuildPurchaseAcknowledgement();
    DocumentSigningCardViewModel BuildDocumentSigning();
}
