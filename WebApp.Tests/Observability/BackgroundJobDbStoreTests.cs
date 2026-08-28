using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Tests;

public sealed class BackgroundJobDbStoreTests
{
    [Fact]
    public void ListQueuedCompanyIds_Returns_Distinct_Companies_In_Due_Order()
    {
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString("N"));
        var store = new BackgroundJobDbStore(factory);
        var companyEarly = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var companyLate = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var utcNow = DateTime.UtcNow;

        store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = companyLate,
            JobType = "excel-import",
            PayloadJson = "{}",
            AvailableAtUtc = utcNow.AddMinutes(-5)
        }, utcNow);

        store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = companyEarly,
            JobType = "excel-import",
            PayloadJson = "{}",
            AvailableAtUtc = utcNow.AddMinutes(-10)
        }, utcNow);

        store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = companyEarly,
            JobType = "flow-engine",
            PayloadJson = "{}",
            AvailableAtUtc = utcNow
        }, utcNow);

        var companyIds = store.ListQueuedCompanyIds(utcNow, take: 10);

        Assert.Equal(new[] { companyEarly, companyLate }, companyIds);
    }

    [Fact]
    public void ListQueuedCompanyIds_Respects_Allowed_Job_Types()
    {
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString("N"));
        var store = new BackgroundJobDbStore(factory);
        var excelCompanyId = Guid.NewGuid();
        var flowCompanyId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = excelCompanyId,
            JobType = "excel-import",
            PayloadJson = "{}",
            AvailableAtUtc = utcNow
        }, utcNow);

        store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = flowCompanyId,
            JobType = "flow-engine",
            PayloadJson = "{}",
            AvailableAtUtc = utcNow
        }, utcNow);

        var companyIds = store.ListQueuedCompanyIds(utcNow, take: 10, allowedJobTypes: new[] { "flow-engine" });

        Assert.Single(companyIds);
        Assert.Equal(flowCompanyId, companyIds[0]);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(string dbName)
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        public ApplicationDbContext CreateDbContext()
            => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
