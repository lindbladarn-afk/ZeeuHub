using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity;

public class ApplicationCompanyJeevesCompany
{
    [Key]
    [Column(TypeName = "uniqueidentifier")]
    public Guid Id { get; set; }

    [Column(TypeName = "uniqueidentifier")]
    public Guid CompanyId { get; set; }

    [Column(TypeName = "int")]
    public int CompanyCode { get; set; }

    [Column(TypeName = "nvarchar(200)")]
    public string DisplayName { get; set; } = string.Empty;

    [Column(TypeName = "bit")]
    public bool IsDefault { get; set; }

    [Column(TypeName = "bit")]
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "int")]
    public int SortOrder { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public ApplicationCompany? Company { get; set; }
}
