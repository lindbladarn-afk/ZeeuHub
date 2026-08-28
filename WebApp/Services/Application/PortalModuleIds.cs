// Defines stable module identifiers shared by menu seeding, access checks, and dashboard visibility.
namespace WebApp.Services.Application;

public static class PortalModuleIds
{
    public static readonly Guid InvoicesSubModule = Guid.Parse("1e4f9e7c-0c1d-4561-9b41-1f1b3f8a0c2c");
    public static readonly Guid OrdersSubModule = Guid.Parse("4aa7f5d3-4d8b-4c76-bb98-8ed1b5d90b22");
    public static readonly Guid BankReconciliationModule = Guid.Parse("c9e7e0a4-8d7c-4d96-9b17-0f09c3d9c0f5");
    public static readonly Guid BankReconciliationSubModule = Guid.Parse("71a97cfc-4c14-4832-9305-a1b85f24126b");
    public static readonly Guid DocumentSigningSubModule = Guid.Parse("b2eec1c0-8d28-45f9-b704-3349f10f6dc1");
}
