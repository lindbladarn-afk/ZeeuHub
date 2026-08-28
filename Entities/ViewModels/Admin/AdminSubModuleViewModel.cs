namespace Entities.ViewModels.Admin;

public class AdminSubModuleViewModel
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MenuItemText { get; set; }
    public Guid? PermissionId { get; set; }
    public bool HasAccess { get; set; } = false;
}
