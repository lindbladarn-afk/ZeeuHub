using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.SuperUser;

// Supplies the company-scoped permission editor for one managed user.
public sealed class UserPermissionsViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string UserDisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool InheritCompanyPermissions { get; set; }

    public List<UserPermissionGroupViewModel> Groups { get; set; } = [];
}

public sealed class UserPermissionGroupViewModel
{
    public Guid ModuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<UserPermissionItemViewModel> Items { get; set; } = [];
}

public sealed class UserPermissionItemViewModel
{
    public Guid ModuleId { get; set; }
    public Guid? SubModuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Selected { get; set; }
}
