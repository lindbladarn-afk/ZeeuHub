using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
    public class ApplicationUserCompanyAccess
    {
        [Column(TypeName = "uniqueidentifier")]
        public Guid Id { get; set; }

        [Column(TypeName = "nvarchar(450)")]
        public string UserId { get; set; } = string.Empty;

        [Column(TypeName = "int")]
        public int CompanyCode { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAtUtc { get; set; }

        public ApplicationUser? User { get; set; }
    }
}
