using Microsoft.Data.SqlClient;
using WebApp.Services.Application;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Encapsulates admin health checks for portal identity and Jeeves data sources.
public sealed class AdminHealthService : IAdminHealthService
{
    private readonly IConfiguration _configuration;
    private readonly IJeevesConnectionResolver _jeevesConnectionResolver;

    public AdminHealthService(IConfiguration configuration, IJeevesConnectionResolver jeevesConnectionResolver)
    {
        _configuration = configuration;
        _jeevesConnectionResolver = jeevesConnectionResolver;
    }

    public async Task<List<AdminOverviewViewModel.HealthStatusItem>> GetHealthAsync()
    {
        var items = GetHealthTemplates();

        var portalCs = _configuration.GetConnectionString("PortalIdentity")
                      ?? Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY");
        items[0] = await CheckSqlAsync("PortalIdentity DB", portalCs);

        var jeevesCs = ResolveOptionalJeevesConnectionString();
        items[1] = await CheckSqlAsync("Jeeves DB", jeevesCs);

        return items;
    }

    public async Task<AdminOverviewViewModel.HealthStatusItem> CheckSqlAsync(string name, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new AdminOverviewViewModel.HealthStatusItem
            {
                Name = name,
                IsHealthy = false,
                Description = "Saknar connection string.",
                Pending = false
            };
        }

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync();

            return new AdminOverviewViewModel.HealthStatusItem
            {
                Name = name,
                IsHealthy = true,
                Description = "OK",
                Pending = false
            };
        }
        catch (Exception ex)
        {
            return new AdminOverviewViewModel.HealthStatusItem
            {
                Name = name,
                IsHealthy = false,
                Description = ex.Message,
                Pending = false
            };
        }
    }

    public List<AdminOverviewViewModel.HealthStatusItem> GetHealthTemplates()
    {
        return new List<AdminOverviewViewModel.HealthStatusItem>
        {
            new() { Name = "PortalIdentity DB", Pending = true, Description = "Ej pingad" },
            new() { Name = "Jeeves DB", Pending = true, Description = "Ej pingad" }
        };
    }

    private string? ResolveOptionalJeevesConnectionString()
    {
        try
        {
            return _jeevesConnectionResolver.ResolveConnectionString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
