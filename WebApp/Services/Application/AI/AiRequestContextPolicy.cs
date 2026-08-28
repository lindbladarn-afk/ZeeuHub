// Applies trusted tenant identity and restricts AI data-source selection by role.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public sealed class AiRequestContextPolicy : IAiRequestContextPolicy
{
    private readonly IAiDataSourceResolver _dataSourceResolver;

    public AiRequestContextPolicy(IAiDataSourceResolver dataSourceResolver)
    {
        _dataSourceResolver = dataSourceResolver;
    }

    public AiRequestContextResult Apply(
        AiQueryRequest request,
        JeevesRuntimeContext runtimeContext,
        bool isAdministrator,
        bool requireTenantDataSource)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        request.CompanyCode = runtimeContext.CompanyCode;
        request.RuntimeConnectionString = runtimeContext.ConnectionString;

        var configuredDataSources = _dataSourceResolver.GetConfiguredDataSources();
        if (configuredDataSources.Count == 0)
            return AiRequestContextResult.Denied("Ingen AI-datakälla är konfigurerad.");

        if (requireTenantDataSource || !isAdministrator)
        {
            var tenantDataSource = configuredDataSources.FirstOrDefault(x => x.IsTenantConnection);
            if (tenantDataSource is null)
                return AiRequestContextResult.Denied("Ingen tenant-datakälla är konfigurerad för AI.");

            request.DataSourceKey = tenantDataSource.Key;
            return AiRequestContextResult.Allowed();
        }

        var requestedKey = (request.DataSourceKey ?? string.Empty).Trim();
        if (requestedKey.Length == 0)
        {
            requestedKey = (_dataSourceResolver.GetSelected() ?? string.Empty).Trim();
        }

        if (requestedKey.Length == 0)
        {
            request.DataSourceKey = configuredDataSources.FirstOrDefault(x => x.IsTenantConnection)?.Key
                ?? configuredDataSources[0].Key;
            return AiRequestContextResult.Allowed();
        }

        var selected = configuredDataSources.FirstOrDefault(x =>
            string.Equals(x.Key, requestedKey, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
            return AiRequestContextResult.Denied("Den valda AI-datakällan är inte tillåten.");

        request.DataSourceKey = selected.Key;
        return AiRequestContextResult.Allowed();
    }
}
