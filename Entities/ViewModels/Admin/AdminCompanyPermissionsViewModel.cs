namespace Entities.ViewModels.Admin;

public class AdminCompanyPermissionsViewModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; }
    public Guid SubModuleId { get; set; }
    public string SubModuleName { get; set; }
}
