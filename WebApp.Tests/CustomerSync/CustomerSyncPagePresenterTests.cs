using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;
using WebApp.Services.Integration.CustomerSync.Presentation;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncPagePresenterTests
{
    [Fact]
    public void Build_Shows_Latest_CustomerSync_Run_Per_Company()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var presenter = new CustomerSyncPagePresenter(
            new CustomerSyncConfigurationPresenter(),
            new FakeBackgroundJobStore(
                new BackgroundJobSnapshot
                {
                    CompanyId = companyId,
                    JobType = CustomerSyncBackgroundJobConstants.ExecuteJobType,
                    Status = BackgroundJobStatus.Completed,
                    CreatedAtUtc = new DateTime(2026, 6, 30, 7, 0, 0, DateTimeKind.Utc),
                    CompletedAtUtc = new DateTime(2026, 6, 30, 7, 2, 0, DateTimeKind.Utc),
                    PayloadJson = new CustomerSyncBackgroundJobPayload
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 7,
                        Direction = CustomerSyncDirection.JeevesToHubSpot,
                        Trigger = CustomerSyncTrigger.Scheduled
                    }.ToJson(),
                    LastResultJson = new CustomerSyncResult
                    {
                        Succeeded = true,
                        Summary = "4 customers created.",
                        CreatedCount = 4,
                        UpdatedCount = 1,
                        SkippedCount = 2,
                        FailedCount = 0
                    }.ToJson()
                },
                new BackgroundJobSnapshot
                {
                    CompanyId = companyId,
                    JobType = "OtherJobType",
                    Status = BackgroundJobStatus.Completed,
                    CreatedAtUtc = new DateTime(2026, 6, 30, 6, 0, 0, DateTimeKind.Utc),
                    CompletedAtUtc = new DateTime(2026, 6, 30, 6, 1, 0, DateTimeKind.Utc),
                    PayloadJson = "{}",
                    LastResultJson = "{}"
                }),
            new FakeCustomerSyncMappingRepository());

        var model = presenter.Build(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.NotNull(company.LatestRun);
        Assert.Equal("Slutförd", company.LatestRun!.StatusLabel);
        Assert.Equal("success", company.LatestRun.StatusTone);
        Assert.Equal("4 customers created.", company.LatestRun.Summary);
        Assert.Equal(4, company.LatestRun.CreatedCount);
        Assert.Equal(1, company.LatestRun.UpdatedCount);
        Assert.Equal(2, company.LatestRun.SkippedCount);
        Assert.Equal(0, company.LatestRun.FailedCount);
        Assert.Equal("Jeeves → HubSpot", company.LatestRun.DirectionLabel);
        Assert.Equal("Schemalagd", company.LatestRun.TriggerLabel);
        Assert.Equal(new DateTime(2026, 6, 30, 7, 2, 0, DateTimeKind.Utc), company.LatestRun.FinishedAtUtc);
    }

    [Fact]
    public void Build_Leaves_Recent_Run_Empty_When_No_CustomerSync_Job_Exists()
    {
        var presenter = new CustomerSyncPagePresenter(
            new CustomerSyncConfigurationPresenter(),
            new FakeBackgroundJobStore(),
            new FakeCustomerSyncMappingRepository());

        var model = presenter.Build(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = Guid.NewGuid(),
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.Null(company.LatestRun);
    }

    [Fact]
    public void Build_Leaves_Recent_Run_Empty_When_Background_Job_Store_Fails()
    {
        var presenter = new CustomerSyncPagePresenter(
            new CustomerSyncConfigurationPresenter(),
            new ThrowingBackgroundJobStore(),
            new FakeCustomerSyncMappingRepository());

        var model = presenter.Build(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = Guid.NewGuid(),
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        });

        var company = Assert.Single(model.Companies);
        Assert.Null(company.LatestRun);
    }

    [Fact]
    public async Task BuildAsync_Shows_Imported_HubSpot_Companies()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var presenter = new CustomerSyncPagePresenter(
            new CustomerSyncConfigurationPresenter(),
            new FakeBackgroundJobStore(),
            new FakeCustomerSyncMappingRepository(
                new CustomerSyncMappingRecord
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 7,
                    HubSpotCompanyId = "12345",
                    OrganizationNumber = "556677-8899",
                    NormalizedName = "Acme AB",
                    Domain = "acme.example",
                    Email = "info@acme.example",
                    Phone = "+468123456",
                    HubSpotUpdatedAtUtc = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc),
                    LastSyncedFromHubSpotAtUtc = new DateTime(2026, 6, 30, 8, 0, 0, DateTimeKind.Utc)
                }));

        var model = await presenter.BuildAsync(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        });

        var imported = Assert.Single(model.ImportedHubSpotCompanies);
        Assert.Equal("12345", imported.HubSpotCompanyId);
        Assert.Equal("Acme AB", imported.Name);
        Assert.Equal("556677-8899", imported.OrganizationNumber);
        Assert.Equal("acme.example", imported.Domain);
        Assert.Equal("info@acme.example", imported.Email);
        Assert.Equal("+468123456", imported.Phone);
        Assert.Equal(new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc), imported.HubSpotUpdatedAtUtc);
    }

    [Fact]
    public async Task BuildAsync_Paginates_Imported_HubSpot_Companies()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var presenter = new CustomerSyncPagePresenter(
            new CustomerSyncConfigurationPresenter(),
            new FakeBackgroundJobStore(),
            new FakeCustomerSyncMappingRepository(
                Enumerable.Range(1, 30)
                    .Select(index => new CustomerSyncMappingRecord
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 7,
                        HubSpotCompanyId = index.ToString(),
                        NormalizedName = $"Company {index:00}",
                        LastSyncedFromHubSpotAtUtc = new DateTime(2026, 6, 30, 8, 0, 0, DateTimeKind.Utc).AddMinutes(index)
                    })
                    .ToArray()));

        var model = await presenter.BuildAsync(new CustomerSyncOptions
        {
            Companies =
            {
                new CustomerSyncCompanyOptions
                {
                    CompanyId = companyId,
                    JeevesCompanyCode = 7,
                    HubSpot = new CustomerSyncHubSpotOptions()
                }
            }
        }, importedCompanyPage: 2);

        Assert.Equal(2, model.ImportedHubSpotCompaniesPagination.Page);
        Assert.Equal(25, model.ImportedHubSpotCompaniesPagination.PageSize);
        Assert.Equal(30, model.ImportedHubSpotCompaniesPagination.TotalCount);
        Assert.Equal(5, model.ImportedHubSpotCompanies.Count);
    }

    private sealed class FakeBackgroundJobStore : IBackgroundJobStore
    {
        private readonly List<BackgroundJobSnapshot> _jobs;

        public FakeBackgroundJobStore(params BackgroundJobSnapshot[] jobs)
        {
            _jobs = jobs.ToList();
        }

        public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null)
            => throw new NotSupportedException();

        public IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null)
            => Array.Empty<Guid>();

        public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId)
            => throw new NotSupportedException();

        public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take)
            => _jobs.Where(job => job.CompanyId == companyId).Take(take).ToList();

        public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null)
            => throw new NotSupportedException();

        public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingBackgroundJobStore : IBackgroundJobStore
    {
        public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null)
            => throw new NotSupportedException();

        public IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null)
            => Array.Empty<Guid>();

        public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId)
            => throw new NotSupportedException();

        public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take)
            => throw new InvalidOperationException("Background job store unavailable.");

        public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null)
            => throw new NotSupportedException();

        public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null)
            => throw new NotSupportedException();

        public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay)
            => throw new NotSupportedException();
    }

    private sealed class FakeCustomerSyncMappingRepository : ICustomerSyncMappingRepository
    {
        private readonly IReadOnlyList<CustomerSyncMappingRecord> _mappings;

        public FakeCustomerSyncMappingRepository(params CustomerSyncMappingRecord[] mappings)
        {
            _mappings = mappings;
        }

        public Task<CustomerSyncMappingRecord?> FindByJeevesCustomerAsync(Guid companyId, int jeevesCompanyCode, string jeevesCustomerNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CustomerSyncMappingRecord?> FindByHubSpotCompanyAsync(Guid companyId, string hubSpotCompanyId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> FindByOrganizationNumberAsync(Guid companyId, string organizationNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> CountHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
            => Task.FromResult(_mappings.Count(item => companyIds.Contains(item.CompanyId)));

        public Task<IReadOnlyList<CustomerSyncMappingRecord>> ListHubSpotMappingsAsync(IReadOnlyCollection<Guid> companyIds, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CustomerSyncMappingRecord>>(
                _mappings
                    .Where(item => companyIds.Contains(item.CompanyId))
                    .OrderByDescending(item => item.LastSyncedFromHubSpotAtUtc ?? item.UpdatedAtUtc)
                    .ThenBy(item => item.NormalizedName)
                    .Skip(skip)
                    .Take(take)
                    .ToList());

        public Task<CustomerSyncMappingRecord> UpsertAsync(CustomerSyncMappingRecord mapping, DateTime utcNow, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
