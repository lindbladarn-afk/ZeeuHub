using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity;

public class ApplicationCompanyConnectionStrings
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column(TypeName = "uniqueidentifier")]
    public Guid Id { get; set; }

    [Column(TypeName = "uniqueidentifier")]
    public Guid CompanyId { get; set; }

    [Column(TypeName = "uniqueidentifier")]
    public Guid ConnectionStringTypeId { get; set; }

    [Column(TypeName = "nvarchar(500)")]
    public string? DatabaseName { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "nvarchar(32)")]
    public string AiDataProfile { get; set; } = "JeevesDirect";

    public bool IsAiEnabled { get; set; }


    // Relations
    [ForeignKey(nameof(CompanyId))]
    public ApplicationCompany? Company { get; set; }

    [ForeignKey(nameof(ConnectionStringTypeId))]
    public ApplicationConnectionStringTypes? ConnectionStringType { get; set; }
}
