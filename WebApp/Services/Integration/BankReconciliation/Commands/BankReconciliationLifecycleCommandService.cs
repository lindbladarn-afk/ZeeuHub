// Validates and applies the close or reopen lifecycle for a reconciliation.
using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Workspace;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

public sealed class BankReconciliationLifecycleCommandService
    : IBankReconciliationLifecycleCommandService
{
    private readonly IBankReconciliationStateService _stateService;
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IBankReconciliationTransactionPageService _transactionPageService;
    private readonly IBankReconciliationWorkspaceService _workspaceService;

    public BankReconciliationLifecycleCommandService(
        IBankReconciliationStateService stateService,
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IBankReconciliationTransactionPageService transactionPageService,
        IBankReconciliationWorkspaceService workspaceService)
    {
        _stateService = stateService;
        _invoiceCandidateService = invoiceCandidateService;
        _transactionPageService = transactionPageService;
        _workspaceService = workspaceService;
    }

    public async Task<BankReconciliationLifecycleCommandResult> CloseAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryResolveIdentity(source, user, out var companyId, out var stateKey, out var failure))
        {
            return failure;
        }

        var current = await _stateService.LoadAsync(companyId, stateKey, cancellationToken);
        if (current.IsClosed)
        {
            return Success(current);
        }

        if (expectedVersion.HasValue && current.Version != expectedVersion.Value)
        {
            return Conflict(current.Version);
        }

        var invoices = await _invoiceCandidateService.LoadAsync(
            source.IsDemoMode,
            user!,
            cancellationToken,
            demoScenarioKey: source.DemoScenarioKey);
        if (!string.IsNullOrWhiteSpace(invoices.ErrorMessage))
        {
            return Failure(invoices.ErrorMessage, current.Version);
        }

        var page = _transactionPageService.BuildPage(
            source.Transactions,
            invoices.Invoices,
            page: 1,
            pageSize: 1,
            filter: "all",
            groupFilter: "all",
            classificationFilter: "all");
        if (page.Summary.Review > 0 || page.Summary.Unmatched > 0)
        {
            return new BankReconciliationLifecycleCommandResult
            {
                Version = current.Version,
                ReviewCount = page.Summary.Review,
                UnmatchedCount = page.Summary.Unmatched,
                ErrorMessage =
                    $"Avstämningen har {page.Summary.Review} poster att granska och " +
                    $"{page.Summary.Unmatched} omatchade poster."
            };
        }

        var codingRules = await _workspaceService.LoadCodingRulesAsync(
            user,
            source,
            cancellationToken);

        try
        {
            var state = await _stateService.CloseAsync(
                companyId,
                stateKey,
                user,
                expectedVersion,
                BankReconciliationStateService.HashStateKey(stateKey),
                codingRules.Version,
                cancellationToken);
            return Success(state);
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex.CurrentVersion);
        }
    }

    public async Task<BankReconciliationLifecycleCommandResult> ReopenAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryResolveIdentity(source, user, out var companyId, out var stateKey, out var failure))
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
        {
            return Failure("Ange en kort orsak till varför avstämningen ska återöppnas.");
        }

        try
        {
            var state = await _stateService.ReopenAsync(
                companyId,
                stateKey,
                user,
                expectedVersion,
                reason,
                cancellationToken);
            return Success(state);
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex.CurrentVersion);
        }
    }

    private static bool TryResolveIdentity(
        BankReconciliationSourceContext source,
        UserSession? user,
        out Guid companyId,
        out string stateKey,
        out BankReconciliationLifecycleCommandResult failure)
    {
        companyId = user?.CompanyId ?? Guid.Empty;
        stateKey = source.StateKey ?? string.Empty;
        failure = new BankReconciliationLifecycleCommandResult();
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(stateKey) || !source.HasSource)
        {
            failure.ErrorMessage = "Ett giltigt avstämningsunderlag krävs.";
            return false;
        }

        return true;
    }

    private static BankReconciliationLifecycleCommandResult Success(
        BankReconciliationPersistedState state)
        => new()
        {
            Success = true,
            Version = state.Version,
            IsClosed = state.IsClosed,
            ClosedAtUtc = state.ClosedAtUtc,
            ClosedByName = state.ClosedByName
        };

    private static BankReconciliationLifecycleCommandResult Conflict(int currentVersion)
        => new()
        {
            Conflict = true,
            Version = currentVersion,
            ErrorMessage =
                "Avstämningen har ändrats av en annan användare. Ladda om och försök igen."
        };

    private static BankReconciliationLifecycleCommandResult Failure(
        string? message,
        int version = 0)
        => new()
        {
            Version = version,
            ErrorMessage = message
        };
}
