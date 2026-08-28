// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class WebApprovalPurchaseOrderVmMap : EntityMap<WebApprovalPurchaseOrderVM>
{
    public WebApprovalPurchaseOrderVmMap()
    {
        Map(u => u.OrderNumber).ToColumn("BestNr");
        Map(u => u.Purchaser).ToColumn("Vref");
        Map(u => u.AttestantPersSign).ToColumn("PersSign2");
        Map(u => u.SupplierNumber).ToColumn("FtgNr");
        Map(u => u.SupplierName).ToColumn("FtgNamn");
        Map(u => u.OrderRegisteredDate).ToColumn("RegDat");
        Map(u => u.OrderEstimatedDeliveryDate).ToColumn("BestBerLevDat");
        Map(u => u.Currency).ToColumn("ValKod");
        Map(u => u.OrderValueLocal).ToColumn("VbBestValue");
        Map(u => u.OrderValue).ToColumn("BestValue");
        Map(u => u.EditExternal).ToColumn("EditExt");
        Map(u => u.ApprovalStatus).ToColumn("ApprovalStatus");
    }
}
