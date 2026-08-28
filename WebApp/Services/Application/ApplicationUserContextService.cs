using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public sealed class ApplicationUserContextService : IApplicationUserContextService
{
    public Task<ApplicationUser?> GetUserByIdAsync(ApplicationDbContext context, string userId)
    {
        return context.Users.FirstOrDefaultAsync(x => x.Id == userId);
    }

    public Task<ApplicationUser?> GetUserByEmailAsync(ApplicationDbContext context, string email)
    {
        return context.Users.FirstOrDefaultAsync(x => x.Email == email);
    }
}
