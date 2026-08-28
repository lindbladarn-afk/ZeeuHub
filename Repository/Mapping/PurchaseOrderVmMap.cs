// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class PurchaseOrderVmMap : EntityMap<PurchaseOrderVM>
{
    public PurchaseOrderVmMap()
    {
        Map(u => u.SupplierNumber).ToColumn("FtgNr");
        Map(u => u.SupplierName).ToColumn("FtgNamn");
        Map(u => u.OrganizationNumber).ToColumn("OrgNr");
        Map(u => u.OrderNumber).ToColumn("BestNr");
        Map(u => u.OrderStatusId).ToColumn("BestStatKod");
        Map(u => u.Co).ToColumn("FtgPostAdr1");
        Map(u => u.Street).ToColumn("FtgPostAdr2");
        Map(u => u.City).ToColumn("FtgPostAdr3");
        Map(u => u.ZipCode).ToColumn("FtgPostNr");
        Map(u => u.Country).ToColumn("Country");
        Map(u => u.CustomerNumberAtSupplier).ToColumn("KundNrHosLev");
        Map(u => u.IsBlocked).ToColumn("IsBlocked");
        Map(u => u.Currency).ToColumn("ValKod");
        Map(u => u.OrderValue).ToColumn("OrderValue");
        Map(u => u.RegisteredDate).ToColumn("RegDat");
        Map(u => u.DeliveryDate).ToColumn("BestBegLevDat");
        Map(u => u.DeliveryCompany).ToColumn("DeliveryFtgNamn");
        Map(u => u.DeliveryCo).ToColumn("DeliveryFtgPostAdr1");
        Map(u => u.DeliveryStreet).ToColumn("DeliveryFtgPostAdr2");
        Map(u => u.DeliveryZip).ToColumn("DeliveryFtgPostNr");
        Map(u => u.DeliveryCity).ToColumn("DeliveryFtgPostAdr3");
        Map(u => u.DeliveryCountry).ToColumn("DeliveryLandsKod");
        Map(u => u.Message).ToColumn("EditExt");
    }
}
