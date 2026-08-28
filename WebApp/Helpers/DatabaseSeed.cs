using Microsoft.AspNetCore.Identity;

namespace WebApp.Helpers
{
    public static class DatabaseSeed
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            // Note, this should not be done in this way in the development environment.
            // In Production it shold be added to the database manually. Not in code.
            if (!await roleManager.RoleExistsAsync("Administrator"))
            {
                await roleManager.CreateAsync(new IdentityRole("Administrator"));
            }
            if (!await roleManager.RoleExistsAsync("SuperUser"))
            {
                await roleManager.CreateAsync(new IdentityRole("SuperUser"));
            }
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }
        }
    }
}
