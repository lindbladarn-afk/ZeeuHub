// Registers the Dapper column mappings used by Jeeves repositories.
namespace Repository.Mapping;

public static class ConfigureMapping
{
    public static void ConfigureDapperMappings()
    {
        FluentMapper.Initialize(config =>
        {
            config.AddMap(new WebApprovalSaleOrderVmMap());
            config.AddMap(new WebApprovalSaleOrderRowVmMap());
            config.AddMap(new WebApprovalPurchaseOrderVmMap());
            config.AddMap(new WebApprovalPurchaseOrderRowVmMap());
            config.AddMap(new JeevesCompanyVmMap());
            config.AddMap(new CustomerVmMap());
            config.AddMap(new CustomerAutocompleteDtoMap());
            config.AddMap(new PurchaseSuppliersAutoCompleteDtoMap());
            config.AddMap(new PurchaseArticleVmMap());
            config.AddMap(new PurchaseSupplierContactVmMap());
            config.AddMap(new ZeeuDashboardProductionDashboardVmMap());
            config.AddMap(new PurchaseOrderVmMap());
        });
    }
}
