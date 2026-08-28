using System.Linq;
using WebApp.Helpers;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public class ConnectionStringResolver : IConnectionStringResolver
{
    private readonly ILogger<ConnectionStringResolver> _logger;

    public ConnectionStringResolver(ILogger<ConnectionStringResolver> logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<string>> ResolveAsync(
    IEnumerable<ApplicationCompanyConnectionStrings> companyConnectionStrings,
    Guid activeConnectionStringId,
    Guid companyId)
    {
        var connectionStrings = companyConnectionStrings?.ToList() ?? new();
        var active = connectionStrings.FirstOrDefault(x => x.Id == activeConnectionStringId);

        if (active is null)
        {
            var message = $"CompanyId: {companyId}: Missing active connection string mapping for {activeConnectionStringId}";
            _logger.LogError(message);
            return OperationResult<string>.Fail(message);
        }

        // Extra hard guard (pattern consistency / future-proofing)
        if (active.CompanyId != companyId)
        {
            var message = $"CompanyId: {companyId}: ActiveConnectionStringId {activeConnectionStringId} does not belong to this company.";
            _logger.LogError(message);
            return OperationResult<string>.Fail(message);
        }

        var envKey = $"CONNECTION_STRING_{active.Id.ToString().ToUpper().Replace("-", string.Empty)}";
        var rawConnectionString = Environment.GetEnvironmentVariable(envKey);

        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            var message = $"CompanyId: {companyId}: Could not find the Environment variable {envKey}";
            _logger.LogError(message);
            return OperationResult<string>.Fail(message);
        }

        var resolved = await KeyVaultHelper.ResolveAsync(rawConnectionString);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            var message = $"CompanyId: {companyId}: Connection string could not be resolved";
            _logger.LogError(message);
            return OperationResult<string>.Fail(message);
        }

        return OperationResult<string>.Ok(resolved);
    }
}