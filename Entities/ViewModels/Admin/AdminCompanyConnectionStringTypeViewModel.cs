namespace Entities.ViewModels.Admin;

public class AdminCompanyConnectionStringTypeViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Type")]
    public string Name { get; set; }

    public string? SuffixName { get; set; }
}
