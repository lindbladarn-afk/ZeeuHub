using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity;

public class ApplicationConnectionStringTypes
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column(TypeName = "uniqueidentifier")]
    public Guid Id { get; set; }

    [Column(TypeName = "nvarchar(250)")]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(250)")]
    public string SuffixName { get; set; } = string.Empty;

    // Relations
    public List<ApplicationCompanyConnectionStrings>? ConnectionStrings { get; set; }
}
