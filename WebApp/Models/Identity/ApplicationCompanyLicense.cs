using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	/// <summary>
	/// The ZeeU products the company is using
	/// </summary>
	public class ApplicationCompanyLicense
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column(TypeName = "uniqueidentifier")]
		public Guid Id { get; set; }

		[Column(TypeName = "uniqueidentifier")]
		public Guid CompanyId { get; set; }

		/// <summary>
		/// Reference to the ZeeU Product Id if applicable
		/// </summary>
		[Column(TypeName = "uniqueidentifier")]
		public Guid ZeeuProductId { get; set; }

		public bool Enabled { get; set; }



		// Relations
		[ForeignKey(nameof(CompanyId))]
		public ApplicationCompany? Company { get; set; }


		[ForeignKey(nameof(ZeeuProductId))]
		public ApplicationZeeuProduct? ZeeuProduct { get; set; }
	}
}
