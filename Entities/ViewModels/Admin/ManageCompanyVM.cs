namespace Entities.ViewModels.Admin;

public class ManageCompanyVM
{
    public string? StatusMessage { get; set; }

    [Display(Name = "Company ID")]
    public Guid Id { get; set; }

    [Display(Name = "Company Name")]
    public string Name { get; set; }
    public int? DefaultJeevesCompanyCode { get; set; }
    public string AiDataProfile { get; set; } = "JeevesDirect";
    public bool AiAllowDataSourceSwitching { get; set; }
    public Guid? AiPrimaryConnectionStringId { get; set; }
    public List<AdminCompanyJeevesCompanyViewModel> JeevesCompanies { get; set; } = new();

    public List<AdminModuleViewModel> AllModules { get; set; }
    public List<AdminCompanyConnectionStringViewModel> ConnectionStrings { get; set; }
    public List<AdminCompanyPermissionsViewModel>? Permissions { get; set; }
    public List<AdminUserViewModel>? Users { get; set; }
}
