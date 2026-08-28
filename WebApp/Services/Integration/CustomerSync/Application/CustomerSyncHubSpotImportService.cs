using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.HubSpot;
using WebApp.Services.Integration.CustomerSync.Mapping;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Imports HubSpot companies into the hub read model without writing anything to Jeeves.
public sealed class CustomerSyncHubSpotImportService : ICustomerSyncHubSpotImportService
{
    private readonly ICustomerSyncRuntimeConfigurationService _runtimeConfigurationService;
    private readonly IHubSpotCustomerClient _hubSpotClient;
    private readonly ICustomerSyncMappingRepository _mappingRepository;
    private readonly ICustomerSyncNormalizer _normalizer;

    public CustomerSyncHubSpotImportService(
        ICustomerSyncRuntimeConfigurationService runtimeConfigurationService,
        IHubSpotCustomerClient hubSpotClient,
        ICustomerSyncMappingRepository mappingRepository,
        ICustomerSyncNormalizer normalizer)
    {
        _runtimeConfigurationService = runtimeConfigurationService;
        _hubSpotClient = hubSpotClient;
        _mappingRepository = mappingRepository;
        _normalizer = normalizer;
    }

    public async Task<CustomerSyncHubSpotImportResult> ImportCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var options = await _runtimeConfigurationService.GetEffectiveOptionsAsync(cancellationToken);
        if (!options.Enabled)
            return new CustomerSyncHubSpotImportResult { Summary = "CustomerSync är inte aktiverad." };

        var enabledCompanies = options.Companies
            .Where(item => item.Enabled)
            .Where(item => item.CompanyId != Guid.Empty && item.JeevesCompanyCode > 0)
            .ToList();

        if (enabledCompanies.Count == 0)
        {
            return new CustomerSyncHubSpotImportResult
            {
                Summary = "Inga aktiva CustomerSync-bolag är konfigurerade."
            };
        }

        var utcNow = DateTime.UtcNow;
        var imported = 0;
        var skipped = 0;

        foreach (var company in enabledCompanies)
        {
            if (string.IsNullOrWhiteSpace(company.HubSpot.Token))
            {
                skipped++;
                continue;
            }

            var hubSpotCompanies = await _hubSpotClient.ListCompaniesAsync(
                new CustomerSyncHubSpotConnection
                {
                    BaseUrl = company.HubSpot.BaseUrl,
                    Token = company.HubSpot.Token
                },
                Math.Max(1, options.BatchSize),
                cancellationToken);

            foreach (var hubSpotCompany in hubSpotCompanies)
            {
                if (string.IsNullOrWhiteSpace(hubSpotCompany.CompanyId))
                {
                    skipped++;
                    continue;
                }

                await _mappingRepository.UpsertAsync(
                    BuildMapping(company, hubSpotCompany, utcNow),
                    utcNow,
                    cancellationToken);
                imported++;
            }
        }

        return new CustomerSyncHubSpotImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped,
            Summary = imported > 0
                ? $"{imported} företag hämtades från HubSpot och visas nu i huben."
                : "Inga företag hämtades från HubSpot. Kontrollera att CustomerSync-bolag och HubSpot-token är konfigurerade."
        };
    }

    private CustomerSyncMappingRecord BuildMapping(
        CustomerSyncCompanyOptions company,
        HubSpotCustomerDto hubSpotCompany,
        DateTime utcNow)
    {
        return new CustomerSyncMappingRecord
        {
            CompanyId = company.CompanyId,
            JeevesCompanyCode = company.JeevesCompanyCode,
            HubSpotCompanyId = hubSpotCompany.CompanyId,
            HubSpotContactId = hubSpotCompany.ContactId,
            OrganizationNumber = _normalizer.NormalizeOrganizationNumber(hubSpotCompany.OrganizationNumber),
            NormalizedName = _normalizer.NormalizeName(hubSpotCompany.Name),
            Domain = hubSpotCompany.Domain,
            Email = _normalizer.NormalizeEmail(hubSpotCompany.Email),
            Phone = _normalizer.NormalizePhone(hubSpotCompany.Phone),
            HubSpotUpdatedAtUtc = hubSpotCompany.UpdatedAtUtc,
            LastSyncedFromHubSpotAtUtc = utcNow
        };
    }
}
