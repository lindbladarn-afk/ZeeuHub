using System.ComponentModel.DataAnnotations;

namespace Entities.ViewModels.Admin;

public class AdminCreateCompanyViewModel
{
    [Required]
    [Display(Name = "Company Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Connection String Type")]
    public Guid ConnectionStringTypeId { get; set; }

    [Required]
    [Display(Name = "Database Name")]
    public string DatabaseName { get; set; } = string.Empty;

    [Display(Name = "Default Jeeves Company Code")]
    public int? DefaultJeevesCompanyCode { get; set; }

    public Guid CompanyId { get; set; }
    public Guid ConnectionStringId { get; set; }
    public string? EnvironmentVariableName { get; set; }
}
