using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationSubModule
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column(TypeName = "uniqueidentifier")]
		public Guid Id { get; set; }

		/// <summary>
		/// The parent module
		/// </summary>
		[Required]
		[Column(TypeName = "uniqueidentifier")]
		public Guid ModuleId { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(250)")]
		public string? Name { get; set; }

		[Column(TypeName = "nvarchar(500)")]
		public string? Description { get; set; }



		// The following properties is the menu items setup
		[Column(TypeName = "nvarchar(100)")]
		[MaxLength(100, ErrorMessage = "The controller name can only be 100 characters")]
		public string? MenuItemController { get; set; }

		[Column(TypeName = "nvarchar(200)")]
		[MaxLength(200, ErrorMessage = "The action name can only be 200 characters")]
		public string? MenuItemAction { get; set; }

		[Column(TypeName = "nvarchar(100)")]
		[MaxLength(100, ErrorMessage = "The menu item name can only be 100 characters")]
		public string? MenuItemText { get; set; }

		/// <summary>
		/// Controlls if the menu item can be clicked of not
		/// </summary>
		public bool? MenuItemEnabled { get; set; }

        public int? MenuItemSortOrder { get; set; }

        // Relations

        [ForeignKey(nameof(ModuleId))]
		public ApplicationModule? Module { get; set; }

		public List<ApplicationCompanyPermission>? Permissions { get; set; }

	}
}
