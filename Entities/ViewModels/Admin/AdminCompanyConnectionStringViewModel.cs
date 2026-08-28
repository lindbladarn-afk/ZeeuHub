namespace Entities.ViewModels.Admin;

public class AdminCompanyConnectionStringViewModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ConnectionStringTypeId { get; set; }

    [Display(Name = "ConnectionString")]
    public string? ConnectionString { get; set; } = null;
    public string? DatabaseName { get; set; }
    public bool IsActive { get; set; }
    public string AiDataProfile { get; set; } = "JeevesDirect";
    public bool IsAiEnabled { get; set; }
    public string? ConnectionStringTypeName { get; set; }

    public AdminCompanyConnectionStringTypeViewModel? ConnectionStringType { get; set; }
}
