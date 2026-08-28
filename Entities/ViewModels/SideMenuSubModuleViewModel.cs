namespace Entities.ViewModels;

public class SideMenuSubModuleViewModel
    {
	public Guid Id { get; set; }
	public Guid ModuleId { get; set; }
	public string? Name { get; set; }
	public string? Description { get; set; }
	public string? MenuItemController { get; set; }
	public string? MenuItemAction { get; set; }
	public bool? MenuItemEnabled { get; set; }
    public int? MenuItemSortOrder { get; set; }
    public bool UserHasPermission { get; set; }
	public string? MenuItemText { get; set; }
    }
