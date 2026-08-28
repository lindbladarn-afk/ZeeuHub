using System;
using System.Collections.Generic;
using System.Linq;
using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.AI;
using WebApp.Services;

namespace WebApp.Services.Application.AI
{
    /// <summary>
    /// Owns the configured AI data-source catalog and resolves approved connections.
    /// </summary>
    public sealed class AiDataSourceResolver : IAiDataSourceResolver
    {
        private const string AiDataSourceSessionKey = "AI_DATASOURCE";
        private const string UserObjectSessionKey = "UserObject";

        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _http;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
        private readonly IDbContextFactory<ApplicationDbContext>? _dbContextFactory;
        private readonly IConnectionStringResolver? _connectionStringResolver;

        public AiDataSourceResolver(
            IConfiguration config,
            IHttpContextAccessor http,
            IJeevesRuntimeContextService jeevesRuntimeContextService,
            IDbContextFactory<ApplicationDbContext>? dbContextFactory = null,
            IConnectionStringResolver? connectionStringResolver = null)
        {
            _config = config;
            _http = http;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
            _dbContextFactory = dbContextFactory;
            _connectionStringResolver = connectionStringResolver;
        }

        public IReadOnlyList<AiDataSourceInfo> GetConfiguredDataSources()
        {
            return Load()
                .Select(ds => new AiDataSourceInfo
                {
                    Key = ds.Key,
                    Name = ds.Name,
                    IsTenantConnection = ds.IsTenantConnection,
                    HasConnectionString = ds.IsTenantConnection || ds.HasAnyExternalConnectionConfigured()
                })
                .ToList();
        }

        public void SetSelected(string key)
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return;

            ctx.Session.SetString(AiDataSourceSessionKey, (key ?? string.Empty).Trim());
        }

        public string? GetSelected()
        {
            return _http.HttpContext?.Session.GetString(AiDataSourceSessionKey);
        }

        public async Task<(string ConnectionString, AiDataSourceInfo Info)> ResolveAsync(string? requestedKey = null, CancellationToken ct = default)
        {
            var companyAiSource = await TryResolveCompanyAiSourceAsync(ct);
            if (companyAiSource is not null)
                return companyAiSource.Value;

            var dataSources = Load();

            // Safety fallback (should never happen)
            if (dataSources.Count == 0)
            {
                dataSources.Add(new AiDataSourceCfg
                {
                    Key = "tenant",
                    Name = "Aktiv tenant",
                    IsTenantConnection = true,
                    IsDefault = true
                });
            }

            var key = PickKey(dataSources, requestedKey);

            // Find selected datasource (never throw)
            var ds = dataSources.FirstOrDefault(x => KeyEquals(x.Key, key))
                     ?? dataSources[0];

            // Resolve connection string
            var tenantConn = await ResolveTenantConnectionAsync(ct);
            var conn = ds.IsTenantConnection
                ? tenantConn
                : ResolveExternalConnection(ds);

            // Only implicit selection may fall back. An explicit tenant selection must never cross tenant boundaries.
            if (string.IsNullOrWhiteSpace(requestedKey) &&
                ds.IsTenantConnection &&
                string.IsNullOrWhiteSpace(conn))
            {
                var fallback = dataSources.FirstOrDefault(x => !x.IsTenantConnection && (x.IsDefault || x.HasAnyExternalConnectionConfigured()));
                if (fallback != null)
                {
                    ds = fallback;
                    conn = ResolveExternalConnection(ds);
                }
            }

            var info = new AiDataSourceInfo
            {
                Key = ds.Key,
                Name = ds.Name,
                IsTenantConnection = ds.IsTenantConnection,
                HasConnectionString = !string.IsNullOrWhiteSpace(conn)
            };
            info.DataProfile = await ResolveCompanyDataProfileAsync(ct);

            if (!string.IsNullOrWhiteSpace(conn))
            {
                try
                {
                    var b = new SqlConnectionStringBuilder(conn);
                    info.Server = b.DataSource;
                    info.Database = b.InitialCatalog;
                }
                catch
                {
                    // ignore
                }
            }

            return (conn, info);
        }

        private async Task<string> ResolveCompanyDataProfileAsync(CancellationToken ct)
        {
            var user = _http.HttpContext?.Session.Get<UserSession>(UserObjectSessionKey);
            if (_dbContextFactory is null || user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return AiDataProfile.JeevesDirect;

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var profile = await db.Companies!
                .AsNoTracking()
                .Where(company => company.Id == companyId)
                .Select(company => company.AiDataProfile)
                .SingleOrDefaultAsync(ct);
            return AiDataProfile.Normalize(profile);
        }

        private async Task<(string ConnectionString, AiDataSourceInfo Info)?> TryResolveCompanyAiSourceAsync(CancellationToken ct)
        {
            var user = _http.HttpContext?.Session.Get<UserSession>(UserObjectSessionKey);
            if (_dbContextFactory is null || _connectionStringResolver is null ||
                user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return null;

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var company = await db.Companies!
                .Include(x => x.ConnectionStrings)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == companyId, ct);
            if (company?.AiPrimaryConnectionStringId is not Guid connectionId || connectionId == Guid.Empty)
                return null;

            var connection = company.ConnectionStrings?.SingleOrDefault(x => x.Id == connectionId && x.IsAiEnabled);
            if (connection is null)
                return null;

            var resolved = await _connectionStringResolver.ResolveAsync(company.ConnectionStrings!, connection.Id, companyId);
            if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Value))
                return null;

            var info = new AiDataSourceInfo
            {
                Key = $"company-ai:{connection.Id:N}",
                Name = connection.DatabaseName ?? "Bolagets AI-datakälla",
                IsTenantConnection = false,
                HasConnectionString = true,
                DataProfile = AiDataProfile.Normalize(connection.AiDataProfile)
            };
            try
            {
                var builder = new SqlConnectionStringBuilder(resolved.Value);
                info.Server = builder.DataSource;
                info.Database = builder.InitialCatalog;
            }
            catch { }

            return (resolved.Value, info);
        }

        private string ResolveExternalConnection(AiDataSourceCfg ds)
        {
            // 1) Prefer explicit ConnectionString from config
            if (!string.IsNullOrWhiteSpace(ds.ConnectionString))
                return ds.ConnectionString!;

            // No implicit fallbacks (keep it deterministic)
            return string.Empty;
        }

        private async Task<string> ResolveTenantConnectionAsync(CancellationToken ct)
        {
            var session = _http.HttpContext?.Session;
            if (session == null) return string.Empty;

            var user = session.Get<UserSession>(UserObjectSessionKey);
            if (user is null)
                return string.Empty;

            var runtimeContext = await _jeevesRuntimeContextService.ResolveAsync(user, ct);
            return runtimeContext.Success && runtimeContext.Value is not null
                ? runtimeContext.Value.ConnectionString
                : string.Empty;
        }

        private List<AiDataSourceCfg> Load()
        {
            // Standard config path: Ai:DataSources
            var section = _config.GetSection("Ai:DataSources");
            var list = new List<AiDataSourceCfg>();

            foreach (var child in section.GetChildren())
            {
                var key = (child["Key"] ?? child.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var name = (child["Name"] ?? child["Key"] ?? child.Key ?? key).Trim();
                var conn = child["ConnectionString"];

                var isTenant = bool.TryParse(child["IsTenantConnection"], out var b) && b;
                var isDefault = bool.TryParse(child["IsDefault"], out var d) && d;

                list.Add(new AiDataSourceCfg
                {
                    Key = key,
                    Name = string.IsNullOrWhiteSpace(name) ? key : name,
                    ConnectionString = string.IsNullOrWhiteSpace(conn) ? null : conn,
                    IsTenantConnection = isTenant,
                    IsDefault = isDefault
                });
            }

            // Default if not configured
            if (list.Count == 0)
            {
                list.Add(new AiDataSourceCfg
                {
                    Key = "tenant",
                    Name = "Aktiv tenant",
                    IsTenantConnection = true,
                    IsDefault = true
                });
            }

            // Ensure unique keys, preserve config order as much as possible
            return list
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private string PickKey(List<AiDataSourceCfg> list, string? requestedKey)
        {
            bool Exists(string? k) =>
                !string.IsNullOrWhiteSpace(k) &&
                list.Any(x => KeyEquals(x.Key, k!.Trim()));

            // 1) requested key
            if (Exists(requestedKey))
                return list.First(x => KeyEquals(x.Key, requestedKey!.Trim())).Key;

            // 2) session key
            var fromSession = _http.HttpContext?.Session.GetString(AiDataSourceSessionKey);
            if (Exists(fromSession))
                return list.First(x => KeyEquals(x.Key, fromSession!.Trim())).Key;

            // 3) IsDefault=true
            var def = list.FirstOrDefault(x => x.IsDefault);
            if (def != null)
                return def.Key;

            // 4) otherwise first
            return list[0].Key;
        }

        private static bool KeyEquals(string a, string b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        private sealed class AiDataSourceCfg
        {
            public string Key { get; set; } = "";
            public string Name { get; set; } = "";
            public string? ConnectionString { get; set; }
            public bool IsTenantConnection { get; set; }
            public bool IsDefault { get; set; }

            public bool HasAnyExternalConnectionConfigured()
            {
                if (!IsTenantConnection && !string.IsNullOrWhiteSpace(ConnectionString))
                    return true;

                return false;
            }
        }
    }
}
