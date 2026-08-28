// Stores versioned bank reconciliation coding matrices in the portal database.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Entities.Application;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration;
using WebApp.Models.Integration.BankReconciliation;

namespace WebApp.Services.Integration.BankReconciliation.CodingRules;

public sealed class BankReconciliationCodingRuleService : IBankReconciliationCodingRuleService
{
    private const int MaxWriteAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public BankReconciliationCodingRuleService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<BankReconciliationCodingRuleSet> LoadAsync(
        Guid companyId,
        string bankAccountKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeBankAccountKey(bankAccountKey);
        var ruleSet = await LoadRuleSetAsync(companyId, normalizedKey, cancellationToken);
        if (string.Equals(normalizedKey, "default", StringComparison.OrdinalIgnoreCase))
        {
            return ruleSet;
        }

        var defaultRuleSet = await LoadRuleSetAsync(companyId, "default", cancellationToken);
        return MergeRuleSets(ruleSet, defaultRuleSet, normalizedKey);
    }

    public async Task<BankReconciliationCodingRuleSet> SaveAsync(
        Guid companyId,
        string bankAccountKey,
        UserSession? user,
        IReadOnlyList<BankReconciliationCodingRuleRow> rows,
        string? bankAccountLabel = null,
        int? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeBankAccountKey(bankAccountKey);
        var keyHash = HashKey(normalizedKey);

        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var record = await context.BankReconciliationCodingRules
                .SingleOrDefaultAsync(
                    item => item.CompanyId == companyId && item.BankAccountKeyHash == keyHash,
                    cancellationToken);
            var ruleSet = record is null
                ? CreateEmptyRuleSet(companyId, normalizedKey)
                : Deserialize(record, companyId, normalizedKey);

            EnsureExpectedVersion(ruleSet, expectedVersion);
            ruleSet.CompanyId = companyId.ToString("D");
            ruleSet.BankAccountKey = normalizedKey;
            ruleSet.BankAccountLabel = string.IsNullOrWhiteSpace(bankAccountLabel)
                ? ruleSet.BankAccountLabel
                : bankAccountLabel.Trim();
            ruleSet.Rows = NormalizeRows(rows, normalizedKey);
            ruleSet.AuditTrail.Add(new BankReconciliationCodingRuleAuditEntry
            {
                ActionType = "save-coding-rules",
                UserId = user?.UserId,
                UserName = BuildUserName(user),
                BankAccountKey = ruleSet.BankAccountKey,
                RowCount = ruleSet.Rows.Count,
                CreatedAtUtc = DateTime.UtcNow
            });
            ruleSet.Version += 1;
            ruleSet.UpdatedAtUtc = DateTime.UtcNow;

            if (record is null)
            {
                record = new BankReconciliationCodingRuleRecord
                {
                    CompanyId = companyId,
                    BankAccountKeyHash = keyHash
                };
                context.BankReconciliationCodingRules.Add(record);
            }

            record.Version = ruleSet.Version;
            record.RuleSetJson = JsonSerializer.Serialize(ruleSet, JsonOptions);
            record.UpdatedAtUtc = ruleSet.UpdatedAtUtc;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return ruleSet;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxWriteAttempts && !expectedVersion.HasValue)
            {
            }
            catch (DbUpdateException) when (record.Version == 1 && attempt < MaxWriteAttempts && !expectedVersion.HasValue)
            {
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BankReconciliationCodingRuleConflictException(
                    await LoadCurrentVersionAsync(companyId, keyHash, cancellationToken));
            }
        }

        throw new BankReconciliationCodingRuleConflictException(
            await LoadCurrentVersionAsync(companyId, keyHash, cancellationToken));
    }

    private async Task<BankReconciliationCodingRuleSet> LoadRuleSetAsync(
        Guid companyId,
        string bankAccountKey,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var keyHash = HashKey(bankAccountKey);
        var record = await context.BankReconciliationCodingRules
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.BankAccountKeyHash == keyHash,
                cancellationToken);

        return record is null
            ? CreateEmptyRuleSet(companyId, bankAccountKey)
            : Deserialize(record, companyId, bankAccountKey);
    }

    private async Task<int> LoadCurrentVersionAsync(
        Guid companyId,
        string keyHash,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BankReconciliationCodingRules
            .Where(item => item.CompanyId == companyId && item.BankAccountKeyHash == keyHash)
            .Select(item => item.Version)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static BankReconciliationCodingRuleSet Deserialize(
        BankReconciliationCodingRuleRecord record,
        Guid companyId,
        string bankAccountKey)
    {
        var ruleSet = JsonSerializer.Deserialize<BankReconciliationCodingRuleSet>(record.RuleSetJson, JsonOptions);
        var normalized = NormalizeRuleSet(ruleSet, companyId, bankAccountKey);
        normalized.Version = record.Version;
        normalized.UpdatedAtUtc = record.UpdatedAtUtc;
        return normalized;
    }

    private static BankReconciliationCodingRuleSet CreateEmptyRuleSet(Guid companyId, string bankAccountKey)
        => new()
        {
            CompanyId = companyId.ToString("D"),
            BankAccountKey = NormalizeBankAccountKey(bankAccountKey)
        };

    private static BankReconciliationCodingRuleSet NormalizeRuleSet(
        BankReconciliationCodingRuleSet? ruleSet,
        Guid companyId,
        string bankAccountKey)
    {
        var normalized = ruleSet ?? new BankReconciliationCodingRuleSet();
        normalized.CompanyId = string.IsNullOrWhiteSpace(normalized.CompanyId)
            ? companyId.ToString("D")
            : normalized.CompanyId;
        normalized.BankAccountKey = string.IsNullOrWhiteSpace(normalized.BankAccountKey)
            ? NormalizeBankAccountKey(bankAccountKey)
            : NormalizeBankAccountKey(normalized.BankAccountKey);
        normalized.Rows = NormalizeRows(normalized.Rows, normalized.BankAccountKey);
        normalized.AuditTrail ??= new List<BankReconciliationCodingRuleAuditEntry>();
        return normalized;
    }

    private static List<BankReconciliationCodingRuleRow> NormalizeRows(
        IEnumerable<BankReconciliationCodingRuleRow>? rows,
        string sourceBankAccountKey)
    {
        return (rows ?? Array.Empty<BankReconciliationCodingRuleRow>())
            .Where(row => row is not null)
            .Select(row => new BankReconciliationCodingRuleRow
            {
                RowId = string.IsNullOrWhiteSpace(row.RowId) ? Guid.NewGuid().ToString("N") : row.RowId,
                TypeKey = NormalizeTypeKey(row.TypeKey),
                TypeLabel = string.IsNullOrWhiteSpace(row.TypeLabel) ? "DEF" : row.TypeLabel.Trim(),
                RuleLabel = string.IsNullOrWhiteSpace(row.RuleLabel) ? "Standard" : row.RuleLabel.Trim(),
                SourceBankAccountKey = NormalizeBankAccountKey(
                    string.IsNullOrWhiteSpace(row.SourceBankAccountKey)
                        ? sourceBankAccountKey
                        : row.SourceBankAccountKey),
                SuggestedAccount = NormalizeOptionalText(row.SuggestedAccount),
                SuggestedCostCenter = NormalizeOptionalText(row.SuggestedCostCenter),
                Account = NormalizeOptionalText(row.Account),
                CostCenter = NormalizeOptionalText(row.CostCenter),
                IsDefault = row.IsDefault ||
                            string.Equals(row.TypeKey, "def", StringComparison.OrdinalIgnoreCase),
                IsInherited = row.IsInherited,
                SortOrder = row.SortOrder,
                Enabled = row.Enabled
            })
            .GroupBy(row => row.TypeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var row = group.First();
                if (string.Equals(row.TypeKey, "def", StringComparison.OrdinalIgnoreCase))
                {
                    row.IsDefault = true;
                    row.TypeLabel = "DEF";
                    row.RuleLabel = "Standard";
                }

                return row;
            })
            .OrderBy(row => row.IsInherited ? 1 : 0)
            .ThenBy(row => row.IsDefault ? 1 : 0)
            .ThenBy(row => row.SortOrder)
            .ThenBy(row => row.TypeLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BankReconciliationCodingRuleSet MergeRuleSets(
        BankReconciliationCodingRuleSet primary,
        BankReconciliationCodingRuleSet fallback,
        string currentBankAccountKey)
    {
        var currentKey = NormalizeBankAccountKey(currentBankAccountKey);
        var merged = new BankReconciliationCodingRuleSet
        {
            CompanyId = primary.CompanyId,
            BankAccountKey = currentKey,
            BankAccountLabel = primary.BankAccountLabel,
            UpdatedAtUtc = primary.UpdatedAtUtc >= fallback.UpdatedAtUtc
                ? primary.UpdatedAtUtc
                : fallback.UpdatedAtUtc,
            Version = primary.Version,
            AuditTrail = primary.AuditTrail.ToList()
        };

        var combined = new List<BankReconciliationCodingRuleRow>();
        combined.AddRange(CloneRows(primary.Rows, currentKey, false));
        foreach (var row in CloneRows(fallback.Rows, fallback.BankAccountKey, true))
        {
            if (combined.Any(existing =>
                    string.Equals(existing.TypeKey, row.TypeKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            combined.Add(row);
        }

        merged.Rows = NormalizeRows(combined, currentKey);
        return merged;
    }

    private static IEnumerable<BankReconciliationCodingRuleRow> CloneRows(
        IEnumerable<BankReconciliationCodingRuleRow> rows,
        string sourceBankAccountKey,
        bool isInherited)
        => rows.Select(row => new BankReconciliationCodingRuleRow
        {
            RowId = row.RowId,
            TypeKey = row.TypeKey,
            TypeLabel = row.TypeLabel,
            RuleLabel = row.RuleLabel,
            SourceBankAccountKey = NormalizeBankAccountKey(sourceBankAccountKey),
            SuggestedAccount = row.SuggestedAccount,
            SuggestedCostCenter = row.SuggestedCostCenter,
            Account = row.Account,
            CostCenter = row.CostCenter,
            IsDefault = row.IsDefault,
            IsInherited = isInherited,
            SortOrder = row.SortOrder,
            Enabled = row.Enabled
        });

    private static void EnsureExpectedVersion(
        BankReconciliationCodingRuleSet ruleSet,
        int? expectedVersion)
    {
        if (expectedVersion.HasValue && ruleSet.Version != expectedVersion.Value)
        {
            throw new BankReconciliationCodingRuleConflictException(ruleSet.Version);
        }
    }

    internal static string HashKey(string value)
    {
        var normalized = NormalizeBankAccountKey(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeBankAccountKey(string value)
        => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToUpperInvariant();

    private static string NormalizeTypeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? "def" : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildUserName(UserSession? user)
    {
        if (user is null)
        {
            return null;
        }

        var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}

public sealed class BankReconciliationCodingRuleConflictException : Exception
{
    public BankReconciliationCodingRuleConflictException(int currentVersion)
        : base("Konteringsreglerna har ändrats av en annan användare eller process. Ladda om och försök igen.")
    {
        CurrentVersion = currentVersion;
    }

    public int CurrentVersion { get; }
}
