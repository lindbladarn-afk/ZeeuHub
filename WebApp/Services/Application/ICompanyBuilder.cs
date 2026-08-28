using Entities.Application;
using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public interface ICompanyBuilder
{
    Task<Company> BuildAsync(ApplicationCompany applicationCompany, ApplicationDbContext context);
}
