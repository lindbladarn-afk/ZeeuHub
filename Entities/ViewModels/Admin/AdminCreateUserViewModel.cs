namespace Entities.ViewModels.Admin;

public class AdminCreateUserViewModel
{
    public string? UserId { get; set; }

    [Required(ErrorMessage = "FirstNameRequired")]
    [DisplayName("FirstName")]

    public string FirstName { get; set; }

    [DisplayName("LastName")]
    public string LastName { get; set; }

    [DisplayName("Email")]
    [Required(ErrorMessage ="EmailRequired")]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; }

    [DisplayName("Perssign")]
    public string? PersSign { get; set; }

    [DisplayName("Language")]
    public string Language { get; set; }

    [DisplayName("PhoneNumber")]
    //[DataType(DataType.PhoneNumber, ErrorMessage = "PhoneNumberValidationError")]
    [Phone(ErrorMessage = "PhoneNumberValidationError")]
    public string? PhoneNumber { get; set; }

    [DisplayName("Company")]
    [Required(ErrorMessage ="CompanyRequired")]
    public Guid CompanyId { get; set; }

    public IEnumerable<string>? Roles { get; set; }

    /// <summary>
    /// List for select list
    /// </summary>
    public IEnumerable<AdminAllCompaniesForSelectListVM>? AllCompanies { get; set; }
}
