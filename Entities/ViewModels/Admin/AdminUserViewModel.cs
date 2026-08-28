using Entities.User;

namespace Entities.ViewModels.Admin;

public class AdminUserViewModel
{
    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string? StatusMessage { get; set; }

    public string UserName { get; set; }

    public string UserId { get; set; }

    [DisplayName("First Name")]
    public string FirstName { get; set; }

    [DisplayName("LastName")]
    public string LastName { get; set; }

    [DisplayName("Email")]
    public string Email { get; set; }

    public bool EmailValidated { get; set; }

    [Display(Name = "PersSign")]
    public string PersSign { get; set; }

    [DisplayName("Whitelist")]
    public bool IsWhitelisted { get; set; }

    [DisplayName("Company")]
    public Guid CompanyId { get; set; }

    [DisplayName("CompanyName")]
    public string CompanyName { get; set; }

    [DisplayName("Active Environment")]
    public Guid? ActiveConnectionStringId { get; set; }

    [DisplayName("Company")]
    public ManageCompanyVM? Company { get; set; }


    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Profile picture")]
    public byte[]? ProfilePicture { get; set; }



    public List<AdminUserRolesViewModel>? Roles { get; set; }

    public IEnumerable<AdminAllCompaniesForSelectListVM>? Companies { get; set; }
    public IEnumerable<AdminCompanyConnectionStringViewModel>? ConnectionStrings { get; set; }
    public IEnumerable<AdminCompanyConnectionStringViewModel>? AllConnectionStrings { get; set; }
    public IEnumerable<AdminCompanyConnectionStringTypeViewModel>? ConnectionStringTypes { get; set; }

    public bool RestrictToAllowedJeevesCompanies { get; set; }
    public List<int> AllowedJeevesCompanyCodes { get; set; } = new();
    public List<JeevesCompanyVM> JeevesCompanies { get; set; } = new();
}
