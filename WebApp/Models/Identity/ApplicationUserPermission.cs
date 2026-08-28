using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity;

// Stores the module access explicitly granted to one user within their company.
public sealed class ApplicationUserPermission
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }

    public Guid ModuleId { get; set; }

    public Guid? SubModuleId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
