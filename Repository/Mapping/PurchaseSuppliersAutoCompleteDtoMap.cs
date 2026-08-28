// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class PurchaseSuppliersAutoCompleteDtoMap : EntityMap<PurchaseSuppliersAutoCompleteDto>
{
    public PurchaseSuppliersAutoCompleteDtoMap()
    {
        Map(u => u.CompanyCode).ToColumn("ForetagKod");
        Map(u => u.OrganizationNumber).ToColumn("OrgNr");
        Map(u => u.SupplierNumber).ToColumn("FtgNr");
        Map(u => u.SupplierName).ToColumn("FtgNamn");
        Map(u => u.City).ToColumn("FtgPostAdr3");
        Map(u => u.Country).ToColumn("Country");
        Map(u => u.IsBlocked).ToColumn("UtbSparr");

    }
}
