// Imports legacy bank reconciliation JSON into SQL without deleting the source files.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration;
using WebApp.Models.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Imports;

namespace WebApp.Seeding;

public static class BankReconciliationLegacyDataMigrator
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web);

    public static async Task MigrateAsync(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        var root = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "Integration",
            "BankReconciliation");
        if (!Directory.Exists(root))
        {
            return;
        }

        await MigrateStatesAsync(context, root, logger, cancellationToken);
        await MigrateImportRegistriesAsync(context, root, logger, cancellationToken);
        await MigrateCodingRulesAsync(context, root, logger, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task MigrateStatesAsync(
        ApplicationDbContext context,
        string root,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var file in EnumerateLegacyFiles(root, "state"))
        {
            try
            {
                var (companyId, keyHash) = ParseIdentity(file);
                if (await context.BankReconciliationStates.AnyAsync(
                        item => item.CompanyId == companyId && item.StateKeyHash == keyHash,
                        cancellationToken))
                {
                    continue;
                }

                var state = await ReadAsync<BankReconciliationPersistedState>(file, cancellationToken);
                if (state is null)
                {
                    continue;
                }

                context.BankReconciliationStates.Add(new BankReconciliationStateRecord
                {
                    CompanyId = companyId,
                    StateKeyHash = keyHash,
                    Version = state.Version,
                    StateJson = JsonSerializer.Serialize(state, WriteOptions),
                    UpdatedAtUtc = state.UpdatedAtUtc
                });
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Kunde inte migrera äldre bankavstämningsstate från {FileName}.", Path.GetFileName(file));
            }
        }
    }

    private static async Task MigrateImportRegistriesAsync(
        ApplicationDbContext context,
        string root,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var file in EnumerateLegacyFiles(root, "imports"))
        {
            try
            {
                var (companyId, accountFingerprint) = ParseIdentity(file);
                if (await context.BankReconciliationImportRegistries.AnyAsync(
                        item => item.CompanyId == companyId &&
                                item.AccountFingerprint == accountFingerprint,
                        cancellationToken))
                {
                    continue;
                }

                var registry = await ReadAsync<BankReconciliationImportRegistryState>(file, cancellationToken);
                if (registry is null)
                {
                    continue;
                }

                context.BankReconciliationImportRegistries.Add(
                    new BankReconciliationImportRegistryRecord
                    {
                        CompanyId = companyId,
                        AccountFingerprint = accountFingerprint,
                        Version = registry.Version,
                        RegistryJson = JsonSerializer.Serialize(registry, WriteOptions),
                        UpdatedAtUtc = File.GetLastWriteTimeUtc(file)
                    });
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Kunde inte migrera äldre importhistorik från {FileName}.", Path.GetFileName(file));
            }
        }
    }

    private static async Task MigrateCodingRulesAsync(
        ApplicationDbContext context,
        string root,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var file in EnumerateLegacyFiles(root, "coding-rules"))
        {
            try
            {
                var (companyId, keyHash) = ParseIdentity(file);
                if (await context.BankReconciliationCodingRules.AnyAsync(
                        item => item.CompanyId == companyId && item.BankAccountKeyHash == keyHash,
                        cancellationToken))
                {
                    continue;
                }

                var ruleSet = await ReadAsync<BankReconciliationCodingRuleSet>(file, cancellationToken);
                if (ruleSet is null)
                {
                    continue;
                }

                context.BankReconciliationCodingRules.Add(new BankReconciliationCodingRuleRecord
                {
                    CompanyId = companyId,
                    BankAccountKeyHash = keyHash,
                    Version = ruleSet.Version,
                    RuleSetJson = JsonSerializer.Serialize(ruleSet, WriteOptions),
                    UpdatedAtUtc = ruleSet.UpdatedAtUtc
                });
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Kunde inte migrera äldre konteringsregler från {FileName}.", Path.GetFileName(file));
            }
        }
    }

    private static IEnumerable<string> EnumerateLegacyFiles(string root, string category)
    {
        var path = Path.Combine(root, category);
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    private static (Guid CompanyId, string KeyHash) ParseIdentity(string file)
    {
        var companyDirectory = Directory.GetParent(file)?.Name;
        var keyHash = Path.GetFileNameWithoutExtension(file);
        if (!Guid.TryParseExact(companyDirectory, "N", out var companyId) ||
            keyHash.Length != 64 ||
            !keyHash.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("Legacyfilens identitet är ogiltig.");
        }

        return (companyId, keyHash.ToUpperInvariant());
    }

    private static async Task<T?> ReadAsync<T>(
        string file,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            ReadOptions,
            cancellationToken);
    }
}
