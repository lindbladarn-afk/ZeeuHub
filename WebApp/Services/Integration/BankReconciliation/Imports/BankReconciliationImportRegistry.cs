// Persists hashed CAMT import history in SQL with optimistic concurrency.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration;
using WebApp.Models.Integration.BankReconciliation;

namespace WebApp.Services.Integration.BankReconciliation.Imports;

public sealed class BankReconciliationImportRegistry : IBankReconciliationImportRegistry
{
    private const int MaxWriteAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public BankReconciliationImportRegistry(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<BankReconciliationImportRegistrationResult> RegisterAsync(
        BankReconciliationImportRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new InvalidOperationException("A company is required when registering a CAMT import.");
        }

        var statement = request.Document.Statements.SingleOrDefault()
            ?? throw new InvalidOperationException("Exactly one CAMT statement is required for import registration.");
        var transactionFingerprints = request.Document.Transactions
            .Select(transaction => transaction.DuplicateFingerprint)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        var statementFingerprint = BankReconciliationImportFingerprint.Statement(statement);
        var documentFingerprint = BankReconciliationImportFingerprint.Document(request.Document);
        var accountFingerprint = BankReconciliationImportFingerprint.Account(statement);

        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var record = await context.BankReconciliationImportRegistries
                .SingleOrDefaultAsync(
                    item => item.CompanyId == request.CompanyId &&
                            item.AccountFingerprint == accountFingerprint,
                    cancellationToken);
            var state = record is null
                ? new BankReconciliationImportRegistryState()
                : Deserialize(record);

            if (state.Imports.Any(import =>
                    string.Equals(
                        import.DocumentFingerprint,
                        documentFingerprint,
                        StringComparison.Ordinal)))
            {
                return Result(BankReconciliationImportStatus.ExactDuplicate, transactionFingerprints.Count);
            }

            var activeImports = state.Imports
                .Where(import => import.SupersededAtUtc is null)
                .ToList();
            var correctedImports = activeImports
                .Where(import => string.Equals(
                    import.StatementFingerprint,
                    statementFingerprint,
                    StringComparison.Ordinal))
                .ToList();
            var overlappingCount = CountOverlap(
                transactionFingerprints,
                activeImports
                    .Except(correctedImports)
                    .SelectMany(import => import.TransactionFingerprints));
            if (correctedImports.Count == 0 && overlappingCount > 0)
            {
                return Result(
                    BankReconciliationImportStatus.Overlapping,
                    transactionFingerprints.Count,
                    overlappingCount);
            }

            var now = DateTime.UtcNow;
            var importRecord = new BankReconciliationImportRecord
            {
                StatementFingerprint = statementFingerprint,
                DocumentFingerprint = documentFingerprint,
                TransactionFingerprints = transactionFingerprints,
                ImportedAtUtc = now
            };
            foreach (var previous in correctedImports)
            {
                previous.SupersededAtUtc = now;
                previous.SupersededByImportId = importRecord.ImportId;
            }

            state.Imports.Add(importRecord);
            state.Version += 1;

            if (record is null)
            {
                record = new BankReconciliationImportRegistryRecord
                {
                    CompanyId = request.CompanyId,
                    AccountFingerprint = accountFingerprint
                };
                context.BankReconciliationImportRegistries.Add(record);
            }

            record.Version = state.Version;
            record.RegistryJson = JsonSerializer.Serialize(state, JsonOptions);
            record.UpdatedAtUtc = now;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result(
                    correctedImports.Count > 0
                        ? BankReconciliationImportStatus.Corrected
                        : BankReconciliationImportStatus.New,
                    transactionFingerprints.Count);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxWriteAttempts)
            {
            }
            catch (DbUpdateException) when (record.Version == 1 && attempt < MaxWriteAttempts)
            {
            }
        }

        throw new InvalidOperationException(
            "CAMT-importen kunde inte registreras eftersom importhistoriken ändrades samtidigt.");
    }

    private static BankReconciliationImportRegistryState Deserialize(
        BankReconciliationImportRegistryRecord record)
    {
        var state = JsonSerializer.Deserialize<BankReconciliationImportRegistryState>(
                        record.RegistryJson,
                        JsonOptions)
                    ?? new BankReconciliationImportRegistryState();
        state.Version = record.Version;
        state.Imports ??= new List<BankReconciliationImportRecord>();
        return state;
    }

    private static int CountOverlap(IEnumerable<string> incoming, IEnumerable<string> existing)
    {
        var incomingCounts = incoming
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var existingCounts = existing
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return incomingCounts.Sum(pair =>
            existingCounts.TryGetValue(pair.Key, out var count)
                ? Math.Min(pair.Value, count)
                : 0);
    }

    private static BankReconciliationImportRegistrationResult Result(
        BankReconciliationImportStatus status,
        int transactionCount,
        int overlappingCount = 0)
        => new()
        {
            Status = status,
            TransactionCount = transactionCount,
            OverlappingTransactionCount = overlappingCount
        };
}
