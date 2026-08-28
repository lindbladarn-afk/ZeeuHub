using System;
using System.Collections.Generic;
using System.Linq;

namespace WebApp.Models.Integration
{
    public class IntegrationOptions
    {
        public List<IntegrationCompanyConfig> Companies { get; set; } = new();
    }

    public class IntegrationCompanyConfig
    {
        public Guid CompanyId { get; set; }
        public int? JeevesCompanyCode { get; set; }
        public bool Enabled { get; set; } = true;
        public List<IntegrationSourceConfig> Sources { get; set; } = new();

        public IntegrationSourceConfig? GetSource(IntegrationSource source)
        {
            return Sources.FirstOrDefault(s => s.Enabled && s.Source == source);
        }
    }

    public class IntegrationSourceConfig
    {
        public IntegrationSource Source { get; set; }
        public string? BaseUrl { get; set; }
        public string? Token { get; set; }
        public string? TestBaseUrl { get; set; }
        public string? TestToken { get; set; }
        public string? AuthUrl { get; set; }
        public string? AppId { get; set; }
        public string? AppSecret { get; set; }
        public string? TestAuthUrl { get; set; }
        public string? TestAppId { get; set; }
        public string? TestAppSecret { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TestUsername { get; set; }
        public string? TestPassword { get; set; }
        public int? GoodsOwnerId { get; set; }
        public int? TestGoodsOwnerId { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed class NamedIntegrationOptions
    {
        public Dictionary<string, NamedIntegrationCompanyConfig> Companies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class NamedIntegrationCompanyConfig
    {
        public Guid CompanyId { get; set; }
        public int? JeevesCompanyCode { get; set; }
        public bool Enabled { get; set; } = true;
        public Dictionary<string, NamedIntegrationSourceConfig> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class NamedIntegrationSourceConfig
    {
        public string? BaseUrl { get; set; }
        public string? Token { get; set; }
        public string? TestBaseUrl { get; set; }
        public string? TestToken { get; set; }
        public string? AuthUrl { get; set; }
        public string? AppId { get; set; }
        public string? AppSecret { get; set; }
        public string? TestAuthUrl { get; set; }
        public string? TestAppId { get; set; }
        public string? TestAppSecret { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TestUsername { get; set; }
        public string? TestPassword { get; set; }
        public int? GoodsOwnerId { get; set; }
        public int? TestGoodsOwnerId { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
