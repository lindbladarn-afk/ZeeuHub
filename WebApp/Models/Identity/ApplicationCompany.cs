using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Identity
{
	public class ApplicationCompany
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		[Column(TypeName = "uniqueidentifier")]
		public Guid Id { get; set; }

		[Column(TypeName = "nvarchar(250)")]
		public string? Name { get; set; }

        [Column(TypeName = "int")]
        public int? DefaultJeevesCompanyCode { get; set; }

        [Column(TypeName = "nvarchar(32)")]
        public string AiDataProfile { get; set; } = "JeevesDirect";

        public bool AiAllowDataSourceSwitching { get; set; }

        [Column(TypeName = "uniqueidentifier")]
        public Guid? AiPrimaryConnectionStringId { get; set; }

        public List<ApplicationCompanyLicense>? Licenses { get; set; }
		public List<ApplicationCompanyPermission>? Permissions { get; set; }
		public List<ApplicationCompanyConnectionStrings>? ConnectionStrings { get; set; }
        public List<ApplicationCompanyJeevesCompany>? JeevesCompanies { get; set; }
	}
}
