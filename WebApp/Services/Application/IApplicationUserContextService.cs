using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

// Reads portal-user context data needed by login and admin flows.
public interface IApplicationUserContextService
{
    Task<ApplicationUser?> GetUserByIdAsync(ApplicationDbContext context, string userId);
    Task<ApplicationUser?> GetUserByEmailAsync(ApplicationDbContext context, string email);
}
