using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineConfigValidationService : IFlowEngineConfigValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IOptions<IntegrationOptions> _integrationOptions;
    private readonly IOptions<AkeneoOptions> _akeneoOptions;

    public FlowEngineConfigValidationService(
        IOptions<IntegrationOptions> integrationOptions,
        IOptions<AkeneoOptions> akeneoOptions)
    {
        _integrationOptions = integrationOptions;
        _akeneoOptions = akeneoOptions;
    }

    public Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        CancellationToken cancellationToken = default)
    {
        var company = _integrationOptions.Value.Companies.FirstOrDefault(entry => entry.CompanyId == runtimeContext.CompanyId);
        var issues = new List<string>();
        var checks = new List<object>();

        if (company is null)
        {
            issues.Add("Integration.Companies saknar aktivt bolag.");
        }
        else
        {
            if (!company.Enabled)
                issues.Add("Bolagets integration-config ar avstangd.");

            if (!company.JeevesCompanyCode.HasValue || company.JeevesCompanyCode.Value <= 0)
                issues.Add("JeevesCompanyCode saknas eller ar ogiltig.");

            checks.Add(ValidateSource(company, IntegrationSource.Jeeves, "BaseUrl", "AuthUrl", "AppId", "AppSecret"));
            checks.Add(ValidateSource(company, IntegrationSource.Centra, "BaseUrl", "Token"));
            checks.Add(ValidateShopifySource(company));
        }

        var akeneoCheck = ValidateAkeneo();
        checks.Add(akeneoCheck);
        issues.AddRange(((IEnumerable<string>)akeneoCheck.GetType().GetProperty("issues")!.GetValue(akeneoCheck)!).Select(issue => $"Akeneo: {issue}"));

        foreach (var check in checks)
        {
            var checkIssues = (IEnumerable<string>)check.GetType().GetProperty("issues")!.GetValue(check)!;
            foreach (var issue in checkIssues)
            {
                var label = (string)check.GetType().GetProperty("source")!.GetValue(check)!;
                issues.Add($"{label}: {issue}");
            }
        }

        var distinctIssues = issues.Distinct(StringComparer.Ordinal).ToList();
        var payload = new
        {
            status = distinctIssues.Count == 0 ? "valid" : "invalid",
            companyId = runtimeContext.CompanyId,
            companyName = runtimeContext.CompanyName,
            companyCode = runtimeContext.CompanyCode,
            configuredCompanies = _integrationOptions.Value.Companies.Select(entry => new
            {
                entry.CompanyId,
                entry.JeevesCompanyCode,
                entry.Enabled,
                sources = entry.Sources.Select(source => new
                {
                    source = source.Source.ToString(),
                    source.Enabled,
                    source.BaseUrl,
                    source.AuthUrl,
                    source.AppId
                })
            }),
            activeCompanySources = company?.Sources.Select(source => new
            {
                source = source.Source.ToString(),
                source.Enabled,
                source.BaseUrl,
                source.AuthUrl,
                source.AppId
            }),
            checks,
            issueCount = distinctIssues.Count,
            issues = distinctIssues
        };

        return Task.FromResult(new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"FlowEngine config validate: Status={(distinctIssues.Count == 0 ? "VALID" : "INVALID")}, Issues={distinctIssues.Count}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                distinctIssues.Count == 0 ? "Alla krav for nuvarande native FlowEngine-yta ar uppfyllda." : "Se JSON-output for full lista over saknade eller ogiltiga configfalt."
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        });
    }

    private static object ValidateSource(IntegrationCompanyConfig company, IntegrationSource source, params string[] requiredFields)
    {
        var config = company.GetSource(source);
        var issues = new List<string>();

        if (config is null)
        {
            issues.Add("Saknas eller ar inte enabled.");
        }
        else
        {
            foreach (var field in requiredFields)
            {
                if (IsMissing(config, field))
                    issues.Add($"{field} saknas.");
            }
        }

        return new
        {
            source = source.ToString(),
            status = issues.Count == 0 ? "valid" : "invalid",
            issues
        };
    }

    private static object ValidateShopifySource(IntegrationCompanyConfig company)
    {
        var config = company.GetSource(IntegrationSource.Shopify);
        var issues = new List<string>();

        if (config is null)
        {
            issues.Add("Saknas eller ar inte enabled.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                issues.Add("BaseUrl saknas.");

            var hasToken = !string.IsNullOrWhiteSpace(config.Token);
            var hasAppCredentials = !string.IsNullOrWhiteSpace(config.AppId) && !string.IsNullOrWhiteSpace(config.AppSecret);
            if (!hasToken && !hasAppCredentials)
                issues.Add("Token eller AppId/AppSecret maste finnas.");
        }

        return new
        {
            source = IntegrationSource.Shopify.ToString(),
            status = issues.Count == 0 ? "valid" : "invalid",
            issues
        };
    }

    private object ValidateAkeneo()
    {
        var options = _akeneoOptions.Value;
        var issues = new List<string>();

        if (!options.Enabled)
            issues.Add("Akeneo ar avstangt.");

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            issues.Add("BaseUrl saknas.");
        if (string.IsNullOrWhiteSpace(options.ClientId))
            issues.Add("ClientId saknas.");
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            issues.Add("ClientSecret saknas.");
        if (string.IsNullOrWhiteSpace(options.Username))
            issues.Add("Username saknas.");
        if (string.IsNullOrWhiteSpace(options.Password))
            issues.Add("Password saknas.");

        return new
        {
            source = "Akeneo",
            status = issues.Count == 0 ? "valid" : "invalid",
            issues
        };
    }

    private static bool IsMissing(IntegrationSourceConfig config, string propertyName)
    {
        var property = typeof(IntegrationSourceConfig).GetProperty(propertyName);
        var value = property?.GetValue(config) as string;
        return string.IsNullOrWhiteSpace(value);
    }
}
