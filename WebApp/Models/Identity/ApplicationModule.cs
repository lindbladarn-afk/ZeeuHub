using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationModule
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column(TypeName = "uniqueidentifier")]
		public Guid Id { get; set; }

		[Column(TypeName = "uniqueidentifier")]
		public Guid? ZeeuProductId { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(250)")]
		public string? Name { get; set; }

		[Column(TypeName = "nvarchar(500)")]
		public string? Description { get; set; }


		// The following properties is the menu sections setup
		/// <summary>
		/// This is the name of the controller this menu section should point to
		/// </summary>
		[Column(TypeName = "nvarchar(100)")]
		public string? MenuSectionController { get; set; }

		[Column(TypeName = "nvarchar(100)")]
        public string? MenuSectionAction { get; set; }

        /// <summary>
        /// The icon shown in front of the menu section name
        /// </summary>
        [Column(TypeName = "nvarchar(100)")]
		public string? MenuSectionIcon { get; set; }

		/// <summary>
		/// The text displayed in the menu
		/// </summary>
		[Column(TypeName = "nvarchar(100)")]
		public string? MenuSectionText { get; set; }

		public bool MenuSectionEnabled { get; set; }

        public int? MenuSectionSortOrder { get; set; }

        // Relations

        [ForeignKey(nameof(ZeeuProductId))]
		public ApplicationZeeuProduct? ZeeuProduct { get; set; }

		public List<ApplicationSubModule>? SubModules { get; set; }
		public List<ApplicationCompanyPermission>? Permissions { get; set; }
	}
}
