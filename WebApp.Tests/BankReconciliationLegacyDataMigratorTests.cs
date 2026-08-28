// Verifies that existing file-backed reconciliation state is retained during SQL migration.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.Integration;
using WebApp.Seeding;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

public sealed class BankReconciliationLegacyDataMigratorTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        "bankrec-legacy-migration",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigrateAsync_ImportsLegacyStateIdempotently()
    {
        const string stateKey = "uploaded:stable-statement";
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var keyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(stateKey)));
        var directory = Path.Combine(
            _contentRoot,
            "App_Data",
            "Integration",
            "BankReconciliation",
            "state",
            companyId.ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, $"{keyHash}.json"),
            JsonSerializer.Serialize(new BankReconciliationPersistedState
            {
                Version = 2,
                Matches =
                {
                    new BankReconciliationSavedMatch
                    {
                        TransactionId = "TX-1",
                        InvoiceId = "INV-1",
                        MatchedAmount = 125m
                    }
                }
            }));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = _contentRoot,
            ContentRootFileProvider = new PhysicalFileProvider(_contentRoot)
        };
        var factory = new TestApplicationDbContextFactory();

        await using (var context = await factory.CreateDbContextAsync())
        {
            await BankReconciliationLegacyDataMigrator.MigrateAsync(
                context,
                environment,
                NullLogger.Instance);
            await BankReconciliationLegacyDataMigrator.MigrateAsync(
                context,
                environment,
                NullLogger.Instance);
        }

        var migrated = await new BankReconciliationStateService(factory)
            .LoadAsync(companyId, stateKey);

        Assert.Equal(2, migrated.Version);
        Assert.Single(migrated.Matches);
        Assert.Equal(125m, migrated.Matches[0].MatchedAmount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
