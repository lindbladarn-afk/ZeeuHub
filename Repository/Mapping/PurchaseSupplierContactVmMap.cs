// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class PurchaseSupplierContactVmMap : EntityMap<PurchaseSupplierContactVM>
{
    public PurchaseSupplierContactVmMap()
    {
        Map(u => u.ContactName).ToColumn("FtgPerson");
        Map(u => u.ContactNumber).ToColumn("ComNr");
        Map(u => u.ContactNumberDescription).ToColumn("ComBeskr");
        Map(u => u.SupplierNumber).ToColumn("FtgNr");
    }
}
