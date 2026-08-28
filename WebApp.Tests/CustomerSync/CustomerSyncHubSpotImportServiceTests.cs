using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.HubSpot;
using WebApp.Services.Integration.CustomerSync.Mapping;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncHubSpotImportServiceTests
{
    [Fact]
    public async Task ImportCompaniesAsync_Saves_HubSpot_Companies_For_Hub_Display()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var repository = new FakeCustomerSyncMappingRepository();
        var service = new CustomerSyncHubSpotImportService(
            new FakeRuntimeConfigurationService(new CustomerSyncOptions
            {
                Enabled = true,
                BatchSize = 50,
                Companies =
                {
                    new CustomerSyncCompanyOptions
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 7,
                        Enabled = true,
                        HubSpot = new CustomerSyncHubSpotOptions
                        {
                            Token = "secret-token"
                        }
                    }
                }
            }),
            new FakeHubSpotCustomerClient(
                new HubSpotCustomerDto
                {
                    CompanyId = "12345",
                    Name = "Acme AB",
                    OrganizationNumber = "556677-8899",
                    Domain = "acme.example",
                    Email = "INFO@ACME.EXAMPLE",
                    Phone = "+46 (0)8 123 456",
                    UpdatedAtUtc = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc)
                }),
            repository,
            new CustomerSyncNormalizer());

        var result = await service.ImportCompaniesAsync();

        Assert.Equal(1, result.ImportedCount);
        var mapping = Assert.Single(repository.Upserted);
        Assert.Equal(companyId, mapping.CompanyId);
        Assert.Equal(7, mapping.JeevesCompanyCode);
        Assert.Equal("12345", mapping.HubSpotCompanyId);
        Assert.Equal("ACME AB", mapping.NormalizedName);
        Assert.Equal("5566778899", mapping.OrganizationNumber);
        Assert.Equal("acme.example", mapping.Domain);
        Assert.Equal("info@acme.example", mapping.Email);
        Assert.Equal("+468123456", mapping.Phone);
        Assert.Equal(new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc), mapping.HubSpotUpdatedAtUtc);
        Assert.NotNull(mapping.LastSyncedFromHubSpotAtUtc);
    }

    private sealed class FakeRuntimeConfigurationService : ICustomerSyncRuntimeConfigurationService
    {
        private readonly CustomerSyncOptions _options;

        public FakeRuntimeConfigurationService(CustomerSyncOptions options)
        {
            _options = options;
        }

        public Task<CustomerSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_options);

        public Task<CustomerSyncRuntimeConfiguration> GetRuntimeConfigurationAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveRuntimeConfigurationAsync(CustomerSyncRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> QueueManualRunsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeHubSpotCustomerClient : IHubSpotCustomerClient
    {
        private readonly IReadOnlyList<HubSpotCustomerDto> _companies;

        public FakeHubSpotCustomerClient(params HubSpotCustomerDto[] companies)
        {
            _companies = companies;
        }

        public Task<IReadOnlyList<HubSpotCustomerDto>> ListCompaniesAsync(CustomerSyncHubSpotConnection connection, int limit, CancellationToken cancellationToken)
            => Task.FromResult(_companies);

        public Task<HubSpotCustomerDto?> GetCompanyAsync(Guid companyId, string hubSpotCompanyId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<HubSpotCustomerWriteResult> UpsertCompanyAsync(Guid companyId, HubSpotCustomerDto customer, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeCustomerSyncMappingRepository : ICustomerSyncMappingRepository
    {
        public List<CustomerSyncMappingRecord> Upserted { get; } = new();

        public Task<CustomerSyncMappingRecord?> FindByJeevesCustomerAsync(Guid companyId, int jeevesCompanyCode, string jeevesCustomerNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CustomerSyncMappingRecord?> FindByHubSpotCompanyAsync(Guid companyId, string hubSpotCompanyId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> FindByOrganizationNumberAsync(Guid companyId, string organizationNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> CountHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> ListHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, int skip, int take, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CustomerSyncMappingRecord> UpsertAsync(CustomerSyncMappingRecord mapping, DateTime utcNow, CancellationToken cancellationToken)
        {
            Upserted.Add(mapping);
            return Task.FromResult(mapping);
        }
    }
}
