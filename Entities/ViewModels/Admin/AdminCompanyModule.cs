namespace Entities.ViewModels.Admin;

public class AdminCompanyModule
{
    public Guid Id { get; set; }
    public Guid? ZeeuProductId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MenuSectionIcon { get; set; }

    public List<AdminCompanySubModule>? SubModules { get; set; }
}
