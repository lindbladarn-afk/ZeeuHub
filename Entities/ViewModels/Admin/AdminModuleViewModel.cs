namespace Entities.ViewModels.Admin;

public class AdminModuleViewModel
{
    public Guid Id { get; set; }
    public Guid? ZeeuProductId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MenuSectionText { get; set; }

    public List<AdminSubModuleViewModel>? SubModules { get; set; }
}
