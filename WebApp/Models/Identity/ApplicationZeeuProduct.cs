using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationZeeuProduct
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column(TypeName = "uniqueidentifier")]
		public Guid Id { get; set; }

		[Required]
		[Column(TypeName = "nvarchar(250)")]
		public string? Name { get; set; }

		[Column(TypeName = "nvarchar(500)")]
		public string? Description { get; set; }

		public byte[]? Image { get; set; }

		public decimal Price { get; set; }

		[Column(TypeName = "nvarchar(500)")]
		public string? Link { get; set; }

		[Column(TypeName = "nvarchar(500)")]
		public string? InternalLink { get; set; }


		// Relational
		public List<ApplicationCompanyLicense>? Licenses { get; set; }
	}
}
