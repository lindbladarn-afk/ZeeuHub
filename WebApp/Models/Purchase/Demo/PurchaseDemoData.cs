using Entities.Purchase;

namespace WebApp.Models.Purchase.Demo;

// Carries the bundled purchase demo orders used when the module is switched to demo mode.
public sealed class PurchaseDemoData
{
    public required List<PurchaseOrderVM> Orders { get; init; }
}
