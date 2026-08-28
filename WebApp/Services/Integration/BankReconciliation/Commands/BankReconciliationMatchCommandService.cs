using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using WebApp.Models.Integration;
using WebApp.Services.Integration;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

// Coordinates match validation and persistence for bank reconciliation commands.
public sealed class BankReconciliationMatchCommandService : IBankReconciliationMatchCommandService
{
    private readonly IBankReconciliationService _bankReconciliationService;
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IBankReconciliationPaymentBundleMatcher _paymentBundleMatcher;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
    private readonly ILogger<BankReconciliationMatchCommandService> _logger;
    private readonly IBankReconciliationMatchEligibilityService _eligibilityService;

    public BankReconciliationMatchCommandService(
        IBankReconciliationService bankReconciliationService,
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IBankReconciliationPaymentBundleMatcher paymentBundleMatcher,
        IHttpContextAccessor contextAccessor,
        IStringLocalizer<SharedResources> sharedLocalizer,
        ILogger<BankReconciliationMatchCommandService> logger,
        IBankReconciliationMatchEligibilityService? eligibilityService = null)
    {
        _bankReconciliationService = bankReconciliationService;
        _invoiceCandidateService = invoiceCandidateService;
        _paymentBundleMatcher = paymentBundleMatcher;
        _contextAccessor = contextAccessor;
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
        _eligibilityService = eligibilityService ?? new BankReconciliationMatchEligibilityService();
    }

    public async Task<BankReconciliationMatchCommandResult> SaveManualMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationManualMatchRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateMatchAsync(source, user, request.TransactionId, request.InvoiceId, request.MatchedAmount, cancellationToken);
        if (!validation.Success || validation.Match is null)
        {
            return validation;
        }

        try
        {
            var state = await _bankReconciliationService.UpsertMatchAsync(
                validation.CompanyId,
                validation.StateKey!,
                user,
                validation.Match,
                request.ExpectedVersion,
                cancellationToken: cancellationToken);

            return new BankReconciliationMatchCommandResult
            {
                Success = true,
                Match = validation.Match,
                Version = state.Version,
                Count = state.Matches.Count
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex);
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return Closed(ex);
        }
    }

    public async Task<BankReconciliationMatchCommandResult> SaveMatchesAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationSaveMatchesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveState(source, user, out var companyId, out var stateKey, out var errorResult))
        {
            return errorResult;
        }

        var validMatches = new List<BankReconciliationSavedMatch>();
        var invoiceRemainingById = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in request.Matches ?? new List<BankReconciliationSavedMatchInput>())
        {
            var validation = await ValidateMatchAsync(source, user, match.TransactionId, match.InvoiceId, match.MatchedAmount, cancellationToken);
            if (!validation.Success || validation.Match is null)
                return Failure(validation.ErrorMessage);

            validation.Match.MatchType = string.IsNullOrWhiteSpace(match.MatchType) ? "auto" : match.MatchType;
            validation.Match.MatchRule = string.IsNullOrWhiteSpace(match.MatchRule) ? validation.Match.MatchRule : match.MatchRule;
            validMatches.Add(validation.Match);
            if (validation.InvoiceRemainingAmount.HasValue)
                invoiceRemainingById[match.InvoiceId] = validation.InvoiceRemainingAmount.Value;
        }

        // Validate the complete payload before replacing persisted state.
        var transactionTotals = validMatches
            .GroupBy(match => match.TransactionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(match => match.MatchedAmount), StringComparer.OrdinalIgnoreCase);
        foreach (var total in transactionTotals)
        {
            var transaction = source.Transactions.FirstOrDefault(item => string.Equals(item.Id, total.Key, StringComparison.OrdinalIgnoreCase));
            if (transaction is not null && total.Value > GetMatchableAmount(transaction))
                return Failure(_sharedLocalizer["BankRec_MatchAmountExceedsTransaction"]);
        }

        var invoiceTotals = validMatches
            .GroupBy(match => match.InvoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(match => match.MatchedAmount), StringComparer.OrdinalIgnoreCase);
        foreach (var total in invoiceTotals)
        {
            if (invoiceRemainingById.TryGetValue(total.Key, out var invoiceRemaining) && total.Value > invoiceRemaining)
                return Failure(_sharedLocalizer["BankRec_MatchAmountExceedsInvoice"]);
        }

        try
        {
            var state = await _bankReconciliationService.ReplaceMatchesAsync(
                companyId,
                stateKey,
                user,
                validMatches,
                auditActionType: "replace-matches",
                expectedVersion: request.ExpectedVersion,
                note: "Synk från bankavstämningens UI.",
                cancellationToken: cancellationToken);

            return new BankReconciliationMatchCommandResult
            {
                Success = true,
                Count = validMatches.Count,
                Version = state.Version,
                Matches = validMatches
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex);
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return Closed(ex);
        }
    }

    public async Task<BankReconciliationMatchCommandResult> ReverseMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationReverseMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveState(source, user, out var companyId, out var stateKey, out var errorResult))
        {
            return errorResult;
        }

        try
        {
            var state = await _bankReconciliationService.ReverseMatchAsync(
                companyId,
                stateKey,
                user,
                request.TransactionId,
                request.AllocationId,
                request.InvoiceId,
                request.ExpectedVersion,
                request.Reason,
                cancellationToken);

            return new BankReconciliationMatchCommandResult
            {
                Success = true,
                Version = state.Version,
                Count = state.Matches.Count
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex);
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return Closed(ex);
        }
    }

    public async Task<BankReconciliationMatchCommandResult> AutoMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryResolveState(source, user, out var companyId, out var stateKey, out var errorResult))
        {
            return errorResult;
        }

        try
        {
            var invoicesResult = await _invoiceCandidateService.LoadAsync(
                source.IsDemoMode,
                user!,
                cancellationToken,
                demoScenarioKey: source.DemoScenarioKey);

            if (!string.IsNullOrWhiteSpace(invoicesResult.ErrorMessage))
            {
                return Failure(invoicesResult.ErrorMessage);
            }

            var transactions = source.Transactions
                .Select(BankReconciliationTransactionPageService.MapTransactionCandidate)
                .ToList();
            var currentState = await _bankReconciliationService.LoadStateAsync(companyId, stateKey, cancellationToken);
            if (expectedVersion.HasValue && currentState.Version != expectedVersion.Value)
            {
                return Conflict(new BankReconciliationStateConflictException(currentState.Version));
            }

            var initialBundleSuggestions = _paymentBundleMatcher
                .BuildSuggestions(transactions, invoicesResult.Invoices, currentState.Matches);
            var bundledTransactionIds = initialBundleSuggestions
                .SelectMany(suggestion => suggestion.Allocations)
                .Select(allocation => allocation.TransactionId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var availableTransactions = BankReconciliationAllocationBalance
                .BuildAvailableTransactions(transactions, currentState.Matches)
                .Where(transaction => !bundledTransactionIds.Contains(transaction.TransactionId))
                .ToList();
            var availableInvoices = BankReconciliationAllocationBalance.BuildAvailableInvoices(invoicesResult.Invoices, currentState.Matches);
            var autoResult = _bankReconciliationService.BuildAutoMatches(availableTransactions, availableInvoices);
            var now = DateTime.UtcNow;
            foreach (var match in autoResult.Matches)
            {
                StampUser(match, user!, now);
            }

            var combinedMatches = currentState.Matches.Concat(autoResult.Matches).ToList();
            var state = currentState;
            if (autoResult.Matches.Count > 0)
            {
                state = await _bankReconciliationService.ReplaceMatchesAsync(
                    companyId,
                    stateKey,
                    user,
                    combinedMatches,
                    auditActionType: "append-auto-matches",
                    expectedVersion: currentState.Version,
                    note: $"Auto-match lade till {autoResult.Matches.Count} matchningar.",
                    cancellationToken: cancellationToken);
            }

            var bundleSuggestions = _paymentBundleMatcher
                .BuildSuggestions(transactions, invoicesResult.Invoices, combinedMatches)
                .ToList();

            return new BankReconciliationMatchCommandResult
            {
                Success = true,
                Count = autoResult.Matches.Count,
                Version = state.Version,
                Matches = combinedMatches,
                PaymentBundleSuggestions = bundleSuggestions
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex);
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return Closed(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-match failed for company {CompanyId}.", companyId);
            return Failure("Auto-matchningen kunde inte slutföras på grund av ett internt fel.");
        }
    }

    public async Task<BankReconciliationMatchCommandResult> ResetMatchesAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryResolveState(source, user, out var companyId, out var stateKey, out var errorResult))
        {
            return errorResult;
        }

        try
        {
            var state = await _bankReconciliationService.ReplaceMatchesAsync(
                companyId,
                stateKey,
                user,
                Array.Empty<BankReconciliationSavedMatch>(),
                auditActionType: "replace-matches",
                expectedVersion: expectedVersion,
                note: "Alla matchningar återställdes i bankavstämningen.",
                cancellationToken: cancellationToken);

            return new BankReconciliationMatchCommandResult
            {
                Success = true,
                Count = 0,
                Version = state.Version
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return Conflict(ex);
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return Closed(ex);
        }
    }

    private async Task<ValidatedBankReconciliationMatchResult> ValidateMatchAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        string transactionId,
        string invoiceId,
        decimal? requestedMatchedAmount,
        CancellationToken cancellationToken)
    {
        if (!TryResolveState(source, user, out var companyId, out var stateKey, out var errorResult))
        {
            return Invalid(errorResult.ErrorMessage, Guid.Empty, null);
        }

        var transaction = source.Transactions.FirstOrDefault(x => string.Equals(x.Id, transactionId, StringComparison.OrdinalIgnoreCase));
        if (transaction is null)
        {
            return Invalid("Transaktionen kunde inte hittas.", companyId, null);
        }

        if (GetMatchableAmount(transaction) <= 0)
        {
            return Invalid(_sharedLocalizer["BankRec_InvalidMatchAmount"], companyId, null);
        }

        var invoicesResult = await _invoiceCandidateService.LoadAsync(
            source.IsDemoMode,
            user!,
            cancellationToken,
            transaction,
            demoScenarioKey: source.DemoScenarioKey);
        if (!string.IsNullOrWhiteSpace(invoicesResult.ErrorMessage))
        {
            return Invalid(invoicesResult.ErrorMessage, companyId, null);
        }

        var invoice = invoicesResult.Invoices.FirstOrDefault(x => string.Equals(x.InvoiceNo, invoiceId, StringComparison.OrdinalIgnoreCase));
        if (invoice is null)
        {
            return Invalid("Fakturan kunde inte hittas.", companyId, null);
        }

        var eligibility = _eligibilityService.Evaluate(
            BankReconciliationTransactionPageService.MapTransactionCandidate(transaction),
            invoice);
        if (!eligibility.IsEligible)
        {
            var reason = eligibility.Rules.First(rule => string.Equals(rule.Status, "blocked", StringComparison.Ordinal)).Message;
            return Invalid(reason, companyId, stateKey);
        }

        var state = await _bankReconciliationService.LoadStateAsync(companyId, stateKey, cancellationToken);
        var alreadyAllocatedOnTransaction = state.Matches
            .Where(x =>
                string.Equals(x.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.InvoiceId, invoiceId, StringComparison.OrdinalIgnoreCase) == false)
            .Sum(x => x.MatchedAmount);
        var transactionRemaining = GetMatchableAmount(transaction) - alreadyAllocatedOnTransaction;
        if (transactionRemaining <= 0)
        {
            return Invalid(_sharedLocalizer["BankRec_TransactionHasNoRemainingAmount"], companyId, stateKey);
        }

        var alreadyAllocated = state.Matches
            .Where(x => !(string.Equals(x.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(x.InvoiceId, invoiceId, StringComparison.OrdinalIgnoreCase)) &&
                        string.Equals(x.InvoiceId, invoiceId, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.MatchedAmount);
        var remaining = invoice.RemainingAmount - alreadyAllocated;

        if (remaining <= 0)
        {
            return Invalid(_sharedLocalizer["BankRec_InvoiceHasNoRemainingAmount"], companyId, stateKey);
        }

        var matchedAmount = requestedMatchedAmount ?? Math.Min(transactionRemaining, remaining);
        if (matchedAmount <= 0)
        {
            return Invalid(_sharedLocalizer["BankRec_InvalidMatchAmount"], companyId, stateKey);
        }

        if (matchedAmount > transactionRemaining)
        {
            return Invalid(_sharedLocalizer["BankRec_MatchAmountExceedsTransaction"], companyId, stateKey);
        }

        if (matchedAmount > remaining)
        {
            return Invalid(_sharedLocalizer["BankRec_MatchAmountExceedsInvoice"], companyId, stateKey);
        }

        var match = new BankReconciliationSavedMatch
        {
            TransactionId = transactionId,
            InvoiceId = invoiceId,
            MatchType = "manual",
            MatchRule = "manual",
            MatchedAmount = matchedAmount,
            Currency = transaction.Currency
        };
        StampUser(match, user!, DateTime.UtcNow);

        return new ValidatedBankReconciliationMatchResult
        {
            Success = true,
            CompanyId = companyId,
            StateKey = stateKey,
            Match = match,
            InvoiceRemainingAmount = remaining
        };
    }

    private bool TryResolveState(
        BankReconciliationSourceContext source,
        UserSession? user,
        out Guid companyId,
        out string stateKey,
        out BankReconciliationMatchCommandResult result)
    {
        companyId = user?.CompanyId ?? Guid.Empty;
        stateKey = source.StateKey ?? string.Empty;
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(stateKey))
        {
            result = Failure(_sharedLocalizer["BankRec_NoActiveSource"]);
            return false;
        }

        result = new BankReconciliationMatchCommandResult { Success = true };
        return true;
    }

    private static void StampUser(BankReconciliationSavedMatch match, UserSession user, DateTime now)
    {
        match.CreatedByUserId = user.UserId;
        match.CreatedByName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        match.CreatedAtUtc = now;
    }

    private static decimal GetMatchableAmount(BankReconciliationParsedTransaction transaction)
        => Math.Abs(transaction.Amount);

    private static BankReconciliationMatchCommandResult Failure(string? errorMessage)
        => new()
        {
            Success = false,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Matchning kunde inte sparas." : errorMessage
        };

    private BankReconciliationMatchCommandResult Conflict(BankReconciliationStateConflictException ex)
        => new()
        {
            Success = false,
            Conflict = true,
            ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                _logger,
                _contextAccessor.HttpContext,
                "BankReconciliationMatch conflict",
                "Matchningen kunde inte sparas på grund av en versionskonflikt.",
                ex),
            CurrentVersion = ex.CurrentVersion
        };

    private static BankReconciliationMatchCommandResult Closed(
        BankReconciliationStateClosedException ex)
        => new()
        {
            Success = false,
            ErrorMessage = ex.Message,
            CurrentVersion = ex.CurrentVersion
        };

    private static ValidatedBankReconciliationMatchResult Invalid(string? errorMessage, Guid companyId, string? stateKey)
        => new()
        {
            Success = false,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Matchning kunde inte sparas." : errorMessage,
            CompanyId = companyId,
            StateKey = stateKey
        };

    private sealed class ValidatedBankReconciliationMatchResult : BankReconciliationMatchCommandResult
    {
        public Guid CompanyId { get; set; }
        public string? StateKey { get; set; }
        public decimal? InvoiceRemainingAmount { get; set; }
    }
}
