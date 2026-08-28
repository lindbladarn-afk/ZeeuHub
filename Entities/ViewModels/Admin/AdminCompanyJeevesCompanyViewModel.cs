namespace Entities.ViewModels.Admin;

public class AdminCompanyJeevesCompanyViewModel
{
    public Guid Id { get; set; }
    public int? CompanyCode { get; set; }
    public string? DisplayName { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
