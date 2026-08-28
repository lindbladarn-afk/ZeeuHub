using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

public sealed class BankReconciliationStateServiceTests
{
    private readonly BankReconciliationStateService _service;

    public BankReconciliationStateServiceTests()
    {
        _service = new BankReconciliationStateService(new TestApplicationDbContextFactory());
    }

    [Fact]
    public async Task UpsertMatch_ReplacesSameTransactionAndInvoiceButKeepsOtherAllocations()
    {
        var companyId = Guid.NewGuid();
        var user = new UserSession { Email = "tester@zeeu.se", FirstName = "Test", LastName = "User" };

        await _service.UpsertMatchAsync(companyId, "demo", user, new BankReconciliationSavedMatch
        {
            TransactionId = "TX-1",
            InvoiceId = "INV-1",
            MatchedAmount = 100m
        });

        await _service.UpsertMatchAsync(companyId, "demo", user, new BankReconciliationSavedMatch
        {
            TransactionId = "TX-1",
            InvoiceId = "INV-2",
            MatchedAmount = 40m
        });

        await _service.UpsertMatchAsync(companyId, "demo", user, new BankReconciliationSavedMatch
        {
            TransactionId = "TX-1",
            InvoiceId = "INV-1",
            MatchedAmount = 90m
        });

        var state = await _service.LoadAsync(companyId, "demo");

        Assert.Equal(2, state.Matches.Count);
        Assert.Equal(3, state.Version);
        Assert.Contains(state.Matches, x => x.InvoiceId == "INV-1" && x.MatchedAmount == 90m);
        Assert.Contains(state.Matches, x => x.InvoiceId == "INV-2" && x.MatchedAmount == 40m);
    }

    [Fact]
    public async Task ReverseMatch_ByAllocationId_RemovesOnlyOneAllocation()
    {
        var companyId = Guid.NewGuid();
        var allocationA = "alloc-a";
        var allocationB = "alloc-b";
        var user = new UserSession { Email = "tester@zeeu.se", FirstName = "Test", LastName = "User" };

        await _service.ReplaceMatchesAsync(companyId, "demo", user, new List<BankReconciliationSavedMatch>
        {
            new() { AllocationId = allocationA, TransactionId = "TX-1", InvoiceId = "INV-1", MatchedAmount = 100m },
            new() { AllocationId = allocationB, TransactionId = "TX-1", InvoiceId = "INV-2", MatchedAmount = 50m }
        }, "replace-matches");

        await _service.ReverseMatchAsync(companyId, "demo", user, "TX-1", allocationId: allocationA, reason: "test");

        var state = await _service.LoadAsync(companyId, "demo");

        Assert.Single(state.Matches);
        Assert.Equal(2, state.Version);
        Assert.Equal(allocationB, state.Matches[0].AllocationId);

        var audit = state.AuditTrail.Last();
        Assert.Equal("reverse-match", audit.ActionType);
        Assert.Equal(100m, audit.MatchedAmount);
    }

    [Fact]
    public async Task UpsertMatch_WithStaleVersion_ThrowsConflict()
    {
        var companyId = Guid.NewGuid();
        var user = new UserSession { Email = "tester@zeeu.se", FirstName = "Test", LastName = "User" };

        var initialState = await _service.UpsertMatchAsync(companyId, "demo", user, new BankReconciliationSavedMatch
        {
            TransactionId = "TX-1",
            InvoiceId = "INV-1",
            MatchedAmount = 100m
        });

        await Assert.ThrowsAsync<BankReconciliationStateConflictException>(() =>
            _service.UpsertMatchAsync(companyId, "demo", user, new BankReconciliationSavedMatch
            {
                TransactionId = "TX-1",
                InvoiceId = "INV-2",
                MatchedAmount = 40m
            }, expectedVersion: initialState.Version - 1));
    }

    [Fact]
    public async Task ReplaceMatches_WithExpectedVersion_ReturnsUpdatedVersion()
    {
        var companyId = Guid.NewGuid();
        var user = new UserSession { Email = "tester@zeeu.se", FirstName = "Test", LastName = "User" };

        var firstState = await _service.ReplaceMatchesAsync(companyId, "demo", user, new List<BankReconciliationSavedMatch>
        {
            new() { TransactionId = "TX-1", InvoiceId = "INV-1", MatchedAmount = 100m }
        }, "replace-matches");

        var secondState = await _service.ReplaceMatchesAsync(companyId, "demo", user, new List<BankReconciliationSavedMatch>
        {
            new() { TransactionId = "TX-1", InvoiceId = "INV-1", MatchedAmount = 80m }
        }, "replace-matches", expectedVersion: firstState.Version);

        Assert.Equal(firstState.Version + 1, secondState.Version);
        Assert.Equal(80m, secondState.Matches.Single().MatchedAmount);
    }

    [Fact]
    public async Task Close_LocksMutations_AndReopenRequiresAuditedReason()
    {
        var companyId = Guid.NewGuid();
        var user = new UserSession
        {
            UserId = "user-1",
            Email = "tester@zeeu.se",
            FirstName = "Test",
            LastName = "User"
        };
        var openState = await _service.ReplaceMatchesAsync(
            companyId,
            "statement-1",
            user,
            new[]
            {
                new BankReconciliationSavedMatch
                {
                    TransactionId = "TX-1",
                    InvoiceId = "INV-1",
                    MatchedAmount = 100m
                }
            },
            "replace-matches");

        var closedState = await _service.CloseAsync(
            companyId,
            "statement-1",
            user,
            openState.Version,
            "SOURCE-HASH",
            codingRulesVersion: 4);

        Assert.True(closedState.IsClosed);
        Assert.Equal("SOURCE-HASH", closedState.ClosedSourceFingerprint);
        Assert.Equal(4, closedState.ClosedCodingRulesVersion);
        await Assert.ThrowsAsync<BankReconciliationStateClosedException>(() =>
            _service.UpsertMatchAsync(
                companyId,
                "statement-1",
                user,
                new BankReconciliationSavedMatch
                {
                    TransactionId = "TX-2",
                    InvoiceId = "INV-2",
                    MatchedAmount = 50m
                },
                closedState.Version));

        var reopenedState = await _service.ReopenAsync(
            companyId,
            "statement-1",
            user,
            closedState.Version,
            "Fakturan korrigerades i Jeeves.");

        Assert.False(reopenedState.IsClosed);
        Assert.Equal("reopen-reconciliation", reopenedState.AuditTrail.Last().ActionType);
        Assert.Equal("Fakturan korrigerades i Jeeves.", reopenedState.AuditTrail.Last().Note);
    }

    [Fact]
    public async Task CloseAndReopen_AreIdempotentWithoutCreatingExtraVersions()
    {
        var companyId = Guid.NewGuid();
        var user = new UserSession { UserId = "user-1", FirstName = "Test", LastName = "User" };
        var openState = await _service.ReplaceMatchesAsync(
            companyId,
            "statement-1",
            user,
            [],
            "replace-matches");

        var closedState = await _service.CloseAsync(
            companyId,
            "statement-1",
            user,
            openState.Version,
            "SOURCE-HASH",
            codingRulesVersion: 1);
        var closedAgain = await _service.CloseAsync(
            companyId,
            "statement-1",
            user,
            expectedVersion: null,
            "SOURCE-HASH",
            codingRulesVersion: 1);
        var reopenedState = await _service.ReopenAsync(
            companyId,
            "statement-1",
            user,
            closedState.Version,
            "Verifierad korrigering.");
        var reopenedAgain = await _service.ReopenAsync(
            companyId,
            "statement-1",
            user,
            expectedVersion: null,
            "Verifierad korrigering.");

        Assert.Equal(closedState.Version, closedAgain.Version);
        Assert.Equal(reopenedState.Version, reopenedAgain.Version);
        Assert.Equal(3, reopenedAgain.AuditTrail.Count);
    }

}
