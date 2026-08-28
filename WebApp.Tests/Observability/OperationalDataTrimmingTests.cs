using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Tests;

public sealed class OperationalDataTrimmingTests
{
    [Fact]
    public async Task BackgroundJobStore_Truncates_LastResultJson_Before_Persisting()
    {
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString("N"));
        var store = new BackgroundJobDbStore(factory);
        var companyId = Guid.NewGuid();
        var workerId = "worker-1";

        var snapshot = store.Enqueue(new BackgroundJobEnqueueRequest
        {
            CompanyId = companyId,
            JobType = "Import",
            PayloadJson = "{}"
        }, DateTime.UtcNow);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var entity = await db.BackgroundJobs!.SingleAsync(item => item.Id == snapshot.Id);
            entity.Status = BackgroundJobStatus.Running.ToString();
            entity.ClaimedBy = workerId;
            entity.ClaimedAtUtc = DateTime.UtcNow;
            entity.StartedAtUtc = DateTime.UtcNow;
            entity.LastHeartbeatAtUtc = DateTime.UtcNow;
            entity.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        }

        var longResult = new string('r', 9000);
        var completed = store.Complete(companyId, snapshot.Id, workerId, DateTime.UtcNow, longResult);

        Assert.Equal(8000, completed.LastResultJson!.Length);
    }

    [Fact]
    public async Task ActionCenterStateStore_Trims_Long_Text_Before_Persisting()
    {
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString("N"));
        var store = new ActionCenterStateStore(factory, NullLogger<ActionCenterStateStore>.Instance);

        await store.UpsertAsync(
            externalId: "insight-1",
            status: ActionCenterItemStatus.Completed,
            companyId: Guid.NewGuid(),
            userId: Guid.NewGuid().ToString("N"),
            snapshot: new ActionCenterUpdateRequest
            {
                Title = new string('t', 500),
                Description = new string('d', 1000),
                Comment = new string('c', 500),
                Category = new string('k', 200)
            },
            cancellationToken: default);

        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.ActionCenterItemStates!.SingleAsync();

        Assert.Equal(256, entity.Title!.Length);
        Assert.Equal(512, entity.Description!.Length);
        Assert.Equal(256, entity.Comment!.Length);
        Assert.Equal(64, entity.Category!.Length);
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
