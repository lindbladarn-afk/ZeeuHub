// Creates the EF Core design-time context from explicitly configured local credentials.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WebApp.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var fromEnv = Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                var ob = new DbContextOptionsBuilder<ApplicationDbContext>();
                ob.UseSqlServer(fromEnv);
                return new ApplicationDbContext(ob.Options);
            }

            var basePath = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = config.GetConnectionString("PortalIdentity")
                     ?? config["CONNECTION_STRING_PORTAL_IDENTITY"];
            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:PortalIdentity with .NET user secrets or CONNECTION_STRING_PORTAL_IDENTITY before running EF Core tooling.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(cs);
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
