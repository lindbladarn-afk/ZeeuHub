namespace Entities.ViewModels;

public class SideMenuModulesViewModel
    {
	public Guid Id { get; set; }
	public string? Name { get; set; }
	public string? Description { get; set; }
	public string? MenuSectionController { get; set; }
    public string? MenuSectionAction { get; set; }
	public string? MenuSectionIcon { get; set; }
     public string? MenuSectionText { get; set; }
	public bool MenuSectionEnabled { get; set; }
    public int? MenuSectionSortOrder { get; set; }
    public bool CompanyHasPermission { get; set; }

	public List<SideMenuSubModuleViewModel>? SubModules { get; set; }
}
