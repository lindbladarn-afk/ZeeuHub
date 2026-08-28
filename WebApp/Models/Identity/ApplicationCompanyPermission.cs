using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationCompanyPermission
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		[Required]
		[Column(TypeName = "uniqueidentifier")]
		public Guid? CompanyId { get; set; }

		[Required]
		[Column(TypeName = "uniqueidentifier")]
		public Guid? ModuleId { get; set; }

		[Column(TypeName = "uniqueidentifier")]
		public Guid? SubModuleId { get; set; }




		// Relations

		[ForeignKey(nameof(CompanyId))]
		public ApplicationCompany? Company { get; set; }

		[ForeignKey(nameof(ModuleId))]
		public ApplicationModule? Module { get; set; }

		[ForeignKey(nameof(SubModuleId))]
		public ApplicationSubModule? SubModule { get; set; }
	}
}