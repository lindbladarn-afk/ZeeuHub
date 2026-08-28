// Provides isolated in-memory portal databases for persistence service tests.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebApp.Data;

namespace WebApp.Tests;

internal sealed class TestApplicationDbContextFactory
    : IDbContextFactory<ApplicationDbContext>
{
    private readonly string _databaseName;
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    public TestApplicationDbContextFactory(string? databaseName = null)
    {
        _databaseName = databaseName ?? Guid.NewGuid().ToString("N");
    }

    public ApplicationDbContext CreateDbContext()
        => new(CreateOptions());

    public Task<ApplicationDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    private DbContextOptions<ApplicationDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot)
            .Options;
}
