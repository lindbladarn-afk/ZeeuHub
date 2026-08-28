using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
    public class ApplicationUserWhitelist
    {
        public Guid Id { get; set; }

        [Column(TypeName = "uniqueidentifier")]
        public Guid? CompanyId { get; set; }

        [Column(TypeName = "nvarchar(450)")]
        public string? UserId { get; set; }

        [Column(TypeName = "nvarchar(256)")]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "nvarchar(500)")]
        public string? Note { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "nvarchar(450)")]
        public string? CreatedByUserId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public ApplicationCompany? Company { get; set; }
    }
}
