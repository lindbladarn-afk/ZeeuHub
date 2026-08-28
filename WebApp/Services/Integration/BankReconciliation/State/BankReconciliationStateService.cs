// Persists bank reconciliation state with optimistic concurrency in the portal database.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Entities.Application;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration;
using WebApp.Models.Integration.BankReconciliation;

namespace WebApp.Services.Integration.BankReconciliation;

public sealed class BankReconciliationStateService : IBankReconciliationStateService
{
    private const int MaxWriteAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public BankReconciliationStateService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<BankReconciliationPersistedState> LoadAsync(
        Guid companyId,
        string stateKey,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var keyHash = HashStateKey(stateKey);
        var record = await context.BankReconciliationStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.StateKeyHash == keyHash,
                cancellationToken);

        return record is null
            ? new BankReconciliationPersistedState()
            : Deserialize(record);
    }

    public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        IReadOnlyList<BankReconciliationSavedMatch> matches,
        string auditActionType,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            companyId,
            stateKey,
            expectedVersion,
            state =>
            {
                EnsureOpen(state);
                state.Matches = matches
                    .OrderBy(item => item.TransactionId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                state.AuditTrail.Add(new BankReconciliationAuditEntry
                {
                    ActionType = auditActionType,
                    UserId = user?.UserId,
                    UserName = BuildUserName(user),
                    CreatedAtUtc = DateTime.UtcNow,
                    Note = note,
                    MatchedAmount = matches.Sum(item => item.MatchedAmount)
                });
                return true;
            },
            cancellationToken);

    public Task<BankReconciliationPersistedState> UpsertMatchAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        BankReconciliationSavedMatch match,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            companyId,
            stateKey,
            expectedVersion,
            state =>
            {
                EnsureOpen(state);
                state.Matches.RemoveAll(item =>
                    string.Equals(item.TransactionId, match.TransactionId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.InvoiceId, match.InvoiceId, StringComparison.OrdinalIgnoreCase));
                state.Matches.Add(match);
                state.AuditTrail.Add(new BankReconciliationAuditEntry
                {
                    ActionType = "manual-match",
                    UserId = user?.UserId,
                    UserName = BuildUserName(user),
                    TransactionId = match.TransactionId,
                    InvoiceId = match.InvoiceId,
                    MatchType = match.MatchType,
                    MatchRule = match.MatchRule,
                    MatchedAmount = match.MatchedAmount,
                    CreatedAtUtc = DateTime.UtcNow,
                    Note = note
                });
                return true;
            },
            cancellationToken);

    public Task<BankReconciliationPersistedState> ReverseMatchAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        string transactionId,
        string? allocationId = null,
        string? invoiceId = null,
        int? expectedVersion = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            companyId,
            stateKey,
            expectedVersion,
            state =>
            {
                EnsureOpen(state);
                var removed = RemoveMatches(state, transactionId, allocationId, invoiceId);
                var first = removed.FirstOrDefault();
                state.AuditTrail.Add(new BankReconciliationAuditEntry
                {
                    ActionType = "reverse-match",
                    UserId = user?.UserId,
                    UserName = BuildUserName(user),
                    TransactionId = first?.TransactionId ?? transactionId,
                    InvoiceId = first?.InvoiceId ?? invoiceId,
                    MatchType = first?.MatchType,
                    MatchRule = first?.MatchRule,
                    MatchedAmount = removed.Sum(item => item.MatchedAmount),
                    CreatedAtUtc = DateTime.UtcNow,
                    Note = reason
                });
                return true;
            },
            cancellationToken);

    public async Task<BankReconciliationPersistedState> CloseAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        int? expectedVersion,
        string sourceFingerprint,
        int codingRulesVersion,
        CancellationToken cancellationToken = default)
    {
        var current = await LoadAsync(companyId, stateKey, cancellationToken);
        if (current.IsClosed)
        {
            return current;
        }

        return await MutateAsync(
            companyId,
            stateKey,
            expectedVersion,
            state =>
            {
                if (state.IsClosed)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                state.IsClosed = true;
                state.ClosedAtUtc = now;
                state.ClosedByUserId = user?.UserId;
                state.ClosedByName = BuildUserName(user);
                state.ClosedSourceFingerprint = sourceFingerprint;
                state.ClosedCodingRulesVersion = codingRulesVersion;
                state.AuditTrail.Add(new BankReconciliationAuditEntry
                {
                    ActionType = "close-reconciliation",
                    UserId = user?.UserId,
                    UserName = BuildUserName(user),
                    CreatedAtUtc = now,
                    Note = $"Avstämningen slutfördes med konteringsregelversion {codingRulesVersion}."
                });
                return true;
            },
            cancellationToken);
    }

    public Task<BankReconciliationPersistedState> ReopenAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        int? expectedVersion,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("En orsak krävs för att återöppna avstämningen.", nameof(reason));
        }

        return MutateAsync(
            companyId,
            stateKey,
            expectedVersion,
            state =>
            {
                if (!state.IsClosed)
                {
                    return false;
                }

                state.IsClosed = false;
                state.ReopenedAtUtc = DateTime.UtcNow;
                state.ReopenedByUserId = user?.UserId;
                state.ReopenedByName = BuildUserName(user);
                state.AuditTrail.Add(new BankReconciliationAuditEntry
                {
                    ActionType = "reopen-reconciliation",
                    UserId = user?.UserId,
                    UserName = BuildUserName(user),
                    CreatedAtUtc = DateTime.UtcNow,
                    Note = reason.Trim()
                });
                return true;
            },
            cancellationToken);
    }

    private async Task<BankReconciliationPersistedState> MutateAsync(
        Guid companyId,
        string stateKey,
        int? expectedVersion,
        Func<BankReconciliationPersistedState, bool> mutation,
        CancellationToken cancellationToken)
    {
        var keyHash = HashStateKey(stateKey);
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var record = await context.BankReconciliationStates
                .SingleOrDefaultAsync(
                    item => item.CompanyId == companyId && item.StateKeyHash == keyHash,
                    cancellationToken);
            var state = record is null
                ? new BankReconciliationPersistedState()
                : Deserialize(record);

            EnsureExpectedVersion(state, expectedVersion);
            if (!mutation(state))
            {
                return state;
            }

            state.Version += 1;
            state.UpdatedAtUtc = DateTime.UtcNow;

            if (record is null)
            {
                record = new BankReconciliationStateRecord
                {
                    CompanyId = companyId,
                    StateKeyHash = keyHash
                };
                context.BankReconciliationStates.Add(record);
            }

            record.Version = state.Version;
            record.StateJson = JsonSerializer.Serialize(state, JsonOptions);
            record.UpdatedAtUtc = state.UpdatedAtUtc;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return state;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxWriteAttempts && !expectedVersion.HasValue)
            {
            }
            catch (DbUpdateException) when (record.Version == 1 && attempt < MaxWriteAttempts && !expectedVersion.HasValue)
            {
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BankReconciliationStateConflictException(
                    await LoadCurrentVersionAsync(companyId, keyHash, cancellationToken));
            }
        }

        throw new BankReconciliationStateConflictException(
            await LoadCurrentVersionAsync(companyId, keyHash, cancellationToken));
    }

    private async Task<int> LoadCurrentVersionAsync(
        Guid companyId,
        string keyHash,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BankReconciliationStates
            .Where(item => item.CompanyId == companyId && item.StateKeyHash == keyHash)
            .Select(item => item.Version)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static List<BankReconciliationSavedMatch> RemoveMatches(
        BankReconciliationPersistedState state,
        string transactionId,
        string? allocationId,
        string? invoiceId)
    {
        var removed = state.Matches
            .Where(item =>
                string.Equals(item.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(allocationId) ||
                 string.Equals(item.AllocationId, allocationId, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(invoiceId) ||
                 string.Equals(item.InvoiceId, invoiceId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        state.Matches.RemoveAll(item => removed.Contains(item));
        return removed;
    }

    private static BankReconciliationPersistedState Deserialize(BankReconciliationStateRecord record)
    {
        var state = JsonSerializer.Deserialize<BankReconciliationPersistedState>(record.StateJson, JsonOptions)
                    ?? new BankReconciliationPersistedState();
        state.Version = record.Version;
        state.UpdatedAtUtc = record.UpdatedAtUtc;
        state.Matches ??= new List<BankReconciliationSavedMatch>();
        state.AuditTrail ??= new List<BankReconciliationAuditEntry>();
        return state;
    }

    internal static string HashStateKey(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static void EnsureExpectedVersion(BankReconciliationPersistedState state, int? expectedVersion)
    {
        if (expectedVersion.HasValue && state.Version != expectedVersion.Value)
        {
            throw new BankReconciliationStateConflictException(state.Version);
        }
    }

    private static void EnsureOpen(BankReconciliationPersistedState state)
    {
        if (state.IsClosed)
        {
            throw new BankReconciliationStateClosedException(state.Version);
        }
    }

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

public sealed class BankReconciliationStateConflictException : Exception
{
    public BankReconciliationStateConflictException(int currentVersion)
        : base("Bankavstämningen har ändrats av en annan användare eller process. Ladda om underlaget och försök igen.")
    {
        CurrentVersion = currentVersion;
    }

    public int CurrentVersion { get; }
}

public sealed class BankReconciliationStateClosedException : Exception
{
    public BankReconciliationStateClosedException(int currentVersion)
        : base("Avstämningen är slutförd och måste återöppnas innan den kan ändras.")
    {
        CurrentVersion = currentVersion;
    }

    public int CurrentVersion { get; }
}
