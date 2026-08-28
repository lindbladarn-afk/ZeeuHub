using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncRuntimeConfigurationServiceTests
{
    [Fact]
    public async Task GetEffectiveOptionsAsync_Overlays_Runtime_Settings_And_Keeps_Secrets()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var service = new CustomerSyncRuntimeConfigurationService(
            new StaticOptionsMonitor<CustomerSyncOptions>(new CustomerSyncOptions
            {
                Enabled = true,
                PollIntervalMinutes = 60,
                BatchSize = 100,
                MaxAttempts = 5,
                WebhookToleranceMinutes = 5,
                Companies =
                {
                    new CustomerSyncCompanyOptions
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 7,
                        Enabled = true,
                        HubSpot = new CustomerSyncHubSpotOptions
                        {
                            BaseUrl = "https://hub.example",
                            Token = "secret-token"
                        }
                    }
                }
            }),
            new FakeRuntimeConfigurationRepository(
                new CustomerSyncRuntimeConfigurationRecord
                {
                    ConfigurationName = "Default",
                    ConfigurationJson = JsonSerializer.Serialize(
                        new CustomerSyncRuntimeConfiguration
                        {
                            Enabled = false,
                            PollIntervalMinutes = 30,
                            BatchSize = 25,
                            MaxAttempts = 3,
                            WebhookToleranceMinutes = 9,
                            Companies =
                            {
                                new CustomerSyncRuntimeCompanyConfiguration
                                {
                                    CompanyId = companyId,
                                    JeevesCompanyCode = 17,
                                    Enabled = false,
                                    HubSpotBaseUrl = "https://runtime.example"
                                }
                            }
                        },
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                }),
            new CustomerSyncJobScheduler(new NoopBackgroundJobStore()),
            NullLogger<CustomerSyncRuntimeConfigurationService>.Instance);

        var options = await service.GetEffectiveOptionsAsync();

        Assert.False(options.Enabled);
        Assert.Equal(30, options.PollIntervalMinutes);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(3, options.MaxAttempts);
        var company = Assert.Single(options.Companies);
        Assert.Equal(17, company.JeevesCompanyCode);
        Assert.False(company.Enabled);
        Assert.Equal("https://runtime.example", company.HubSpot.BaseUrl);
        Assert.Equal("secret-token", company.HubSpot.Token);
    }

    [Fact]
    public async Task QueueManualRunsAsync_Queues_Manual_Triggers_For_Enabled_Companies()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var store = new NoopBackgroundJobStore();
        var service = new CustomerSyncRuntimeConfigurationService(
            new StaticOptionsMonitor<CustomerSyncOptions>(new CustomerSyncOptions
            {
                Enabled = true,
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
            new FakeRuntimeConfigurationRepository(),
            new CustomerSyncJobScheduler(store),
            NullLogger<CustomerSyncRuntimeConfigurationService>.Instance);

        var queued = await service.QueueManualRunsAsync();

        Assert.Equal(1, queued);
        var job = Assert.Single(store.Enqueued);
        var payload = CustomerSyncBackgroundJobPayload.FromJson(job.PayloadJson);
        Assert.Equal(CustomerSyncTrigger.Manual, payload.Trigger);
        Assert.Equal(CustomerSyncDirection.JeevesToHubSpot, payload.Direction);
    }

    [Fact]
    public async Task QueueManualRunsAsync_Does_Not_Double_Queue_Same_Company_When_Named_Alias_Exists()
    {
        var companyId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var store = new NoopBackgroundJobStore();
        var service = new CustomerSyncRuntimeConfigurationService(
            new StaticOptionsMonitor<CustomerSyncOptions>(new CustomerSyncOptions
            {
                Enabled = true,
                Companies =
                {
                    new CustomerSyncCompanyOptions
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 9900,
                        Enabled = true,
                        HubSpot = new CustomerSyncHubSpotOptions
                        {
                            Token = "secret-token"
                        }
                    }
                },
                NamedCompanies =
                {
                    ["ZeeU"] = new CustomerSyncCompanyOptions
                    {
                        CompanyId = companyId,
                        JeevesCompanyCode = 9900,
                        Enabled = true,
                        HubSpot = new CustomerSyncHubSpotOptions
                        {
                            Token = "secret-token"
                        }
                    }
                }
            }),
            new FakeRuntimeConfigurationRepository(),
            new CustomerSyncJobScheduler(store),
            NullLogger<CustomerSyncRuntimeConfigurationService>.Instance);

        var queued = await service.QueueManualRunsAsync();

        Assert.Equal(1, queued);
        Assert.Single(store.Enqueued);
    }

    [Fact]
    public async Task GetEffectiveOptionsAsync_Falls_Back_To_App_Settings_When_Runtime_Store_Fails()
    {
        var service = new CustomerSyncRuntimeConfigurationService(
            new StaticOptionsMonitor<CustomerSyncOptions>(new CustomerSyncOptions
            {
                Enabled = true,
                PollIntervalMinutes = 60,
                BatchSize = 100,
                MaxAttempts = 5
            }),
            new ThrowingRuntimeConfigurationRepository(),
            new CustomerSyncJobScheduler(new NoopBackgroundJobStore()),
            NullLogger<CustomerSyncRuntimeConfigurationService>.Instance);

        var options = await service.GetEffectiveOptionsAsync();

        Assert.True(options.Enabled);
        Assert.Equal(60, options.PollIntervalMinutes);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(5, options.MaxAttempts);
    }

    private sealed class FakeRuntimeConfigurationRepository : ICustomerSyncRuntimeConfigurationRepository
    {
        private readonly CustomerSyncRuntimeConfigurationRecord? _record;

        public FakeRuntimeConfigurationRepository(CustomerSyncRuntimeConfigurationRecord? record = null)
        {
            _record = record;
        }

        public Task<CustomerSyncRuntimeConfigurationRecord?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_record);

        public Task<CustomerSyncRuntimeConfigurationRecord> UpsertAsync(CustomerSyncRuntimeConfigurationRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);
    }

    private sealed class ThrowingRuntimeConfigurationRepository : ICustomerSyncRuntimeConfigurationRepository
    {
        public Task<CustomerSyncRuntimeConfigurationRecord?> GetAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("runtime table missing");

        public Task<CustomerSyncRuntimeConfigurationRecord> UpsertAsync(CustomerSyncRuntimeConfigurationRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticOptionsMonitor<T> : Microsoft.Extensions.Options.IOptionsMonitor<T> where T : class, new()
    {
        public StaticOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class NoopBackgroundJobStore : IBackgroundJobStore
    {
        public List<BackgroundJobSnapshot> Enqueued { get; } = new();

        public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow)
        {
            var snapshot = new BackgroundJobSnapshot
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                JobType = request.JobType,
                CorrelationKey = request.CorrelationKey,
                PayloadJson = request.PayloadJson,
                MaxAttempts = request.MaxAttempts,
                Status = BackgroundJobStatus.Queued,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
                QueuedAtUtc = utcNow,
                AvailableAtUtc = request.AvailableAtUtc ?? utcNow
            };

            Enqueued.Add(snapshot);
            return snapshot;
        }

        public BackgroundJobSnapshot? TryClaimNext(string workerId, DateTime utcNow, TimeSpan leaseDuration, Guid? companyId = null, IReadOnlyCollection<string>? allowedJobTypes = null) => throw new NotSupportedException();
        public IReadOnlyList<Guid> ListQueuedCompanyIds(DateTime utcNow, int take, IReadOnlyCollection<string>? allowedJobTypes = null) => Array.Empty<Guid>();
        public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null) => null;
        public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId) => throw new NotSupportedException();
        public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take) => throw new NotSupportedException();
        public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take) => throw new NotSupportedException();
        public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration) => throw new NotSupportedException();
        public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null) => throw new NotSupportedException();
        public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null) => throw new NotSupportedException();
        public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null) => throw new NotSupportedException();
        public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay) => throw new NotSupportedException();
    }
}
