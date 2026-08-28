// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class CustomerVmMap : EntityMap<CustomerViewModel>
{
    public CustomerVmMap()
    {
        Map(u => u.CustomerNumber).ToColumn("ForetagKod");
        Map(u => u.CompanyName).ToColumn("FtgNamn");
        Map(u => u.OrganizationNumber).ToColumn("OrgNr");
        Map(u => u.SalesPerson).ToColumn("SaljareNamn");
        Map(u => u.Co).ToColumn("FtgPostAdr1");
        Map(u => u.Street).ToColumn("FtgPostAdr2");
        Map(u => u.City).ToColumn("FtgPostAdr3");
        Map(u => u.ZipCode).ToColumn("FtgPostNr");
        Map(u => u.Country).ToColumn("Country");
        Map(u => u.NetTurnoverLastYear).ToColumn("KundOmsFa");
        Map(u => u.NetTurnoverThisYear).ToColumn("KundOms");
    }
}
