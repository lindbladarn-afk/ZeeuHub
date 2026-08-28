// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class CustomerAutocompleteDtoMap : EntityMap<CustomersAutoCompleteDto>
{
    public CustomerAutocompleteDtoMap()
    {
        Map(u => u.CustomerNumber).ToColumn("ForetagKod");
        Map(u => u.CompanyName).ToColumn("FtgNamn");
        Map(u => u.OrganizationNumber).ToColumn("OrgNr");
        Map(u => u.City).ToColumn("FtgPostAdr3");
    }
}
