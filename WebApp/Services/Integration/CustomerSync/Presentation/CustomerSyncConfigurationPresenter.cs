using System.Text.RegularExpressions;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync;
using WebApp.ViewModels.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Presentation;

// Builds a small, non-secret status snapshot for the CustomerSync hub page.
public sealed class CustomerSyncConfigurationPresenter
{
    private static readonly Regex KeyVaultReferencePattern = new(
        @"^\s*@Microsoft\.KeyVault\s*\(\s*SecretUri\s*=\s*(?<uri>[^)]+)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public CustomerSyncPageViewModel Build(CustomerSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CustomerSyncPageViewModel
        {
            IsEnabled = options.Enabled,
            PollIntervalMinutes = options.PollIntervalMinutes,
            BatchSize = options.BatchSize,
            MaxAttempts = options.MaxAttempts,
            Companies = CustomerSyncCompanyCatalog.GetCompanies(options)
                .Select(item => BuildCompany(item.DisplayName, item.Company))
                .ToArray()
        };
    }

    private static CustomerSyncCompanyStatusViewModel BuildCompany(string displayName, CustomerSyncCompanyOptions company)
    {
        var token = BuildTokenStatus(company.HubSpot.Token);
        return new CustomerSyncCompanyStatusViewModel
        {
            DisplayName = displayName,
            CompanyId = company.CompanyId,
            JeevesCompanyCode = company.JeevesCompanyCode,
            Enabled = company.Enabled,
            HasHubSpotConnection = token.IsConfigured,
            HubSpotConnectionLabel = token.IsConfigured ? "Kontakt finns" : "Ingen kontakt",
            HubSpotConnectionTone = token.IsConfigured ? "success" : "danger",
            HubSpotBaseUrl = Normalize(company.HubSpot.BaseUrl),
            HubSpotToken = token
        };
    }

    private static CustomerSyncHubSpotTokenViewModel BuildTokenStatus(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new CustomerSyncHubSpotTokenViewModel
            {
                IsConfigured = false,
                StatusLabel = "Saknas",
                StatusTone = "danger",
                SourceLabel = "Ingen token är konfigurerad"
            };
        }

        var trimmed = rawToken.Trim();
        if (TryExtractSecretName(trimmed, out var secretName))
        {
            return new CustomerSyncHubSpotTokenViewModel
            {
                IsConfigured = true,
                StatusLabel = "Key Vault-referens",
                StatusTone = "success",
                SecretName = secretName,
                SourceLabel = "Läst från Azure Key Vault"
            };
        }

        return new CustomerSyncHubSpotTokenViewModel
        {
            IsConfigured = true,
            StatusLabel = "Konfigurerad",
            StatusTone = "success",
            SourceLabel = "Lagrad direkt i konfigurationen"
        };
    }

    private static bool TryExtractSecretName(string value, out string? secretName)
    {
        secretName = null;

        var match = KeyVaultReferencePattern.Match(value);
        var secretUri = match.Success
            ? match.Groups["uri"].Value.Trim().Trim('"', '\'')
            : value;

        if (!Uri.TryCreate(secretUri, UriKind.Absolute, out var uri))
            return false;

        var segments = uri.Segments
            .Select(segment => segment.Trim('/'))
            .Where(segment => segment.Length > 0)
            .ToArray();

        if (segments.Length < 2 || !segments[0].Equals("secrets", StringComparison.OrdinalIgnoreCase))
            return false;

        secretName = segments[1];
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
