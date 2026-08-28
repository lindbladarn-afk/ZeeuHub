// Maps Jeeves query columns to portal models for Dapper.
namespace Repository.Mapping;

internal class JeevesCompanyVmMap : EntityMap<JeevesCompanyVM>
{
    public JeevesCompanyVmMap()
    {
        Map(u => u.CompanyCode).ToColumn("ForetagKod");
        Map(u => u.Name).ToColumn("Name");
        Map(u => u.IsDefault).ToColumn("IsDefault");
    }
}
