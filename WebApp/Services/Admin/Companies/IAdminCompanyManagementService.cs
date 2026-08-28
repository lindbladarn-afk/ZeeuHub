using Entities.User;
using Entities.ViewModels.Admin;

namespace WebApp.Services.Admin;

public interface IAdminCompanyManagementService
{
    Task<IReadOnlyCollection<ManageCompanyVM>> GetCompaniesAsync();
    AdminCreateCompanyViewModel BuildCreateCompanyViewModel();
    Task<AdminCreateCompanyResult> CreateCompanyAsync(AdminCreateCompanyViewModel model);
    Task<ManageCompanyVM?> GetManageCompanyAsync(Guid companyId);
    Task<AdminConnectionTestResult> TestCompanyConnectionAsync(Guid companyId, Guid connectionStringId);
    Task<AdminJeevesCompaniesResult> GetJeevesCompaniesAsync(Guid companyId, Guid connectionStringId, string persSign);
    Task<AdminManageCompanyResult> UpdateCompanyAsync(ManageCompanyVM model);
}

public sealed class AdminCreateCompanyResult
{
    public AdminCreateCompanyViewModel Model { get; init; } = new();
    public bool ShouldReturnView { get; init; }
    public Guid CreatedCompanyId { get; init; }
    public string? SuccessMessage { get; init; }
}

public sealed class AdminConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class AdminJeevesCompaniesResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyCollection<JeevesCompanyVM> Items { get; init; } = Array.Empty<JeevesCompanyVM>();
}

public sealed class AdminManageCompanyResult
{
    public bool RedirectToCompanies { get; init; }
    public ManageCompanyVM? Model { get; set; }
    public List<string> SuccessMessages { get; init; } = new();
    public List<string> ErrorMessages { get; init; } = new();
}
