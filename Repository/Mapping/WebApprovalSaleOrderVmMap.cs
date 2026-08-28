// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class WebApprovalSaleOrderVmMap : EntityMap<WebApprovalSaleOrderVM>
{
    internal WebApprovalSaleOrderVmMap()
    {
        Map(u => u.OrderNumber).ToColumn("OrderNr");
        Map(u => u.OrderType).ToColumn("OrderTyp");
        Map(u => u.SalesReference).ToColumn("Vref");
        //Map(u => u.Approver).ToColumn("PersSign2");
        Map(u => u.CustomerNumber).ToColumn("FtgNr");
        Map(u => u.CustomerName).ToColumn("FtgNamn");
        Map(u => u.OrderRegisteredDate).ToColumn("RegDat");
        Map(u => u.OrderEstimatedDeliveryDate).ToColumn("OrdBerLevDat");
        Map(u => u.Currency).ToColumn("ValKod");
        Map(u => u.OrderValueLocal).ToColumn("VbOrdSum");
        Map(u => u.OrderValue).ToColumn("OrdSum");

    }
}
