using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationUser : IdentityUser
	{
		[Required]
		[PersonalData]
		[Column(TypeName = "nvarchar(100)")]
		public string? FirstName { get; set; }

		[Required]
		[PersonalData]
		[Column(TypeName = "nvarchar(100)")]
		public string? LastName { get; set; }

		[Column(TypeName = "nvarchar(50)")]
		public string? PersSign { get; set; }

		[Column(TypeName = "nvarchar(50)")]
		public string? Language { get; set; }

		[Column(TypeName = "uniqueidentifier")]
		public Guid? CompanyId { get; set; }

		public byte[]? ProfilePicture { get; set; }

        [Column(TypeName = "uniqueidentifier")]
        public Guid? ActiveConnectionStringId { get; set; }


        // Relational
        [ForeignKey(nameof(ActiveConnectionStringId))]
        public ApplicationCompanyConnectionStrings? ActiveConnectionString { get; set; }


        //      public bool NotificationsToPortal { get; set; }

        //[PersonalData]
        //      public string PushoverId { get; set; }
        // Relations

        // The user CompanyId needs to be stored here as well
        // An instance of the company should be added here when the user is logged in


		[ForeignKey(nameof(CompanyId))]
		public ApplicationCompany? Company { get; set; }

        public ICollection<ApplicationUserCompanyAccess> AllowedCompanyCodes { get; set; } = new List<ApplicationUserCompanyAccess>();

        public bool UseCustomPermissions { get; set; }

        public ICollection<ApplicationUserPermission> Permissions { get; set; } = new List<ApplicationUserPermission>();

	}
}
