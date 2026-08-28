using Entities.Application;
using Microsoft.AspNetCore.Http;
using WebApp.Models.Integration;
using WebApp.Services.Integration;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;

namespace WebApp.Services.Integration.BankReconciliation.Bundles;

// Revalidates deterministic payment bundles against current company state before one atomic write.
public sealed class BankReconciliationPaymentBundleService : IBankReconciliationPaymentBundleService
{
    private readonly IBankReconciliationService _bankReconciliationService;
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IBankReconciliationPaymentBundleMatcher _bundleMatcher;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<BankReconciliationPaymentBundleService> _logger;
    private readonly IBankReconciliationMatchEligibilityService _eligibilityService;

    public BankReconciliationPaymentBundleService(
        IBankReconciliationService bankReconciliationService,
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IBankReconciliationPaymentBundleMatcher bundleMatcher,
        IHttpContextAccessor contextAccessor,
        ILogger<BankReconciliationPaymentBundleService> logger,
        IBankReconciliationMatchEligibilityService? eligibilityService = null)
    {
        _bankReconciliationService = bankReconciliationService;
        _invoiceCandidateService = invoiceCandidateService;
        _bundleMatcher = bundleMatcher;
        _contextAccessor = contextAccessor;
        _logger = logger;
        _eligibilityService = eligibilityService ?? new BankReconciliationMatchEligibilityService();
    }

    public async Task<BankReconciliationPaymentBundleQueryResult> BuildSuggestionsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(source, user, cancellationToken);
        if (!context.Success)
        {
            return new BankReconciliationPaymentBundleQueryResult
            {
                Success = false,
                ErrorMessage = context.ErrorMessage
            };
        }

        var availableTransactions = BankReconciliationAllocationBalance
            .BuildAvailableTransactions(context.Transactions!, context.State!.Matches)
            .Where(IsCustomerPaymentCandidate)
            .OrderByDescending(transaction => transaction.ValueDate ?? transaction.Date, StringComparer.OrdinalIgnoreCase)
            .ThenBy(transaction => transaction.TransactionId, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();
        var availableInvoices = BankReconciliationAllocationBalance
            .BuildAvailableInvoices(context.Invoices!, context.State.Matches)
            .OrderBy(invoice => invoice.DueDate)
            .ThenBy(invoice => invoice.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        return new BankReconciliationPaymentBundleQueryResult
        {
            Version = context.State.Version,
            Suggestions = _bundleMatcher
                .BuildSuggestions(context.Transactions!, context.Invoices!, context.State.Matches)
                .ToList(),
            AvailableTransactions = availableTransactions
                .Select(transaction => new BankReconciliationManualPaymentTransaction
                {
                    TransactionId = transaction.TransactionId,
                    Date = transaction.ValueDate ?? transaction.Date,
                    DebtorName = transaction.DebtorName,
                    Reference = transaction.Reference,
                    RemainingAmount = transaction.Amount,
                    Currency = string.IsNullOrWhiteSpace(transaction.Currency) ? "SEK" : transaction.Currency
                })
                .ToList(),
            AvailableInvoices = availableInvoices
                .Select(invoice => new BankReconciliationManualPaymentInvoice
                {
                    InvoiceId = invoice.InvoiceNo,
                    InvoiceNo = invoice.InvoiceNo,
                    Ocr = string.IsNullOrWhiteSpace(invoice.Ocr) ? null : invoice.Ocr,
                    CustomerName = invoice.Customer,
                    RemainingAmount = invoice.RemainingAmount,
                    Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "SEK" : invoice.Currency
                })
                .ToList()
        };
    }

    public async Task<BankReconciliationPaymentBundleCommandResult> ConfirmAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationConfirmPaymentBundleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BundleId))
            return Failure("Betalningsgruppen saknar identitet.");

        if (!request.ExpectedVersion.HasValue)
            return Failure("Betalningsgruppen saknar state-version och måste laddas om.");

        var context = await LoadContextAsync(source, user, cancellationToken);
        if (!context.Success)
            return Failure(context.ErrorMessage);

        if (context.State!.Version != request.ExpectedVersion.Value)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                Conflict = true,
                CurrentVersion = context.State.Version,
                ErrorMessage = "Bankavstämningens state har ändrats. Ladda om underlaget och granska betalningsgruppen igen."
            };
        }

        var suggestion = _bundleMatcher
            .BuildSuggestions(context.Transactions!, context.Invoices!, context.State.Matches)
            .SingleOrDefault(item => string.Equals(item.BundleId, request.BundleId, StringComparison.Ordinal));
        if (suggestion is null)
            return Failure("Betalningsgruppen är inte längre ett giltigt förslag. Ladda om underlaget och granska igen.");

        var now = DateTime.UtcNow;
        var matches = suggestion.Allocations.Select(allocation => new BankReconciliationSavedMatch
        {
            AllocationId = Guid.NewGuid().ToString("N"),
            TransactionId = allocation.TransactionId,
            InvoiceId = suggestion.InvoiceId,
            MatchType = "manual",
            MatchRule = "payment-bundle",
            MatchedAmount = allocation.MatchedAmount,
            Currency = allocation.Currency,
            CreatedByUserId = user!.UserId,
            CreatedByName = BuildUserName(user),
            CreatedAtUtc = now
        }).ToList();

        try
        {
            var updatedState = await _bankReconciliationService.ReplaceMatchesAsync(
                context.CompanyId,
                context.StateKey!,
                user,
                context.State.Matches.Concat(matches).ToList(),
                auditActionType: "confirm-payment-bundle",
                expectedVersion: request.ExpectedVersion.Value,
                note: $"Bekräftad betalningsgrupp med {matches.Count} transaktioner mot faktura {suggestion.InvoiceNo}.",
                cancellationToken: cancellationToken);

            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = true,
                Version = updatedState.Version,
                Matches = matches
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                Conflict = true,
                CurrentVersion = ex.CurrentVersion,
                ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _contextAccessor.HttpContext,
                    "BankReconciliationPaymentBundle conflict",
                    "Betalningsgruppen kunde inte sparas på grund av en versionskonflikt.",
                ex)
            };
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                CurrentVersion = ex.CurrentVersion,
                ErrorMessage = ex.Message
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment bundle confirmation failed for company {CompanyId}.", context.CompanyId);
            return Failure("Betalningsgruppen kunde inte sparas på grund av ett internt fel.");
        }
    }

    public async Task<BankReconciliationPaymentBundleCommandResult> ConfirmManualAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        BankReconciliationConfirmManualPaymentBundleRequest request,
        CancellationToken cancellationToken)
    {
        var transactionIds = (request.TransactionIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (string.IsNullOrWhiteSpace(request.InvoiceId))
            return Failure("Välj en faktura för betalningsgruppen.");
        if (transactionIds.Count < 2)
            return Failure("Välj minst två betalningar för att skapa en grupp.");
        if (transactionIds.Count > 20)
            return Failure("En manuell betalningsgrupp får innehålla högst 20 betalningar.");
        if (!request.ExpectedVersion.HasValue)
            return Failure("Betalningsgruppen saknar state-version och måste laddas om.");

        var context = await LoadContextAsync(source, user, cancellationToken);
        if (!context.Success)
            return Failure(context.ErrorMessage);
        if (context.State!.Version != request.ExpectedVersion.Value)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                Conflict = true,
                CurrentVersion = context.State.Version,
                ErrorMessage = "Bankavstämningens state har ändrats. Ladda om underlaget och bygg gruppen igen."
            };
        }

        var availableTransactions = BankReconciliationAllocationBalance
            .BuildAvailableTransactions(context.Transactions!, context.State.Matches)
            .Where(IsCustomerPaymentCandidate)
            .ToDictionary(transaction => transaction.TransactionId, StringComparer.OrdinalIgnoreCase);
        var availableInvoices = BankReconciliationAllocationBalance
            .BuildAvailableInvoices(context.Invoices!, context.State.Matches);
        var invoice = availableInvoices.FirstOrDefault(item =>
            string.Equals(item.InvoiceNo, request.InvoiceId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (invoice is null)
            return Failure("Fakturan saknas eller har inte något kvarvarande belopp.");

        var selectedTransactions = new List<BankReconciliationTransactionCandidate>();
        foreach (var transactionId in transactionIds)
        {
            if (!availableTransactions.TryGetValue(transactionId, out var transaction) || transaction.Amount <= 0m)
                return Failure($"Betalningen {transactionId} saknas eller har inte något kvarvarande belopp.");

            var eligibility = _eligibilityService.Evaluate(transaction, invoice);
            if (!eligibility.IsEligible)
            {
                var reason = eligibility.Rules
                    .First(rule => string.Equals(rule.Status, "blocked", StringComparison.Ordinal))
                    .Message;
                return Failure($"{transactionId}: {reason}");
            }

            selectedTransactions.Add(transaction);
        }

        var total = selectedTransactions.Sum(transaction => transaction.Amount);
        if (total > invoice.RemainingAmount)
            return Failure("Betalningsgruppens summa överstiger fakturans kvarvarande belopp.");

        var now = DateTime.UtcNow;
        var matches = selectedTransactions.Select(transaction => new BankReconciliationSavedMatch
        {
            AllocationId = Guid.NewGuid().ToString("N"),
            TransactionId = transaction.TransactionId,
            InvoiceId = invoice.InvoiceNo,
            MatchType = "manual",
            MatchRule = "manual-payment-bundle",
            MatchedAmount = transaction.Amount,
            Currency = string.IsNullOrWhiteSpace(transaction.Currency) ? "SEK" : transaction.Currency,
            CreatedByUserId = user!.UserId,
            CreatedByName = BuildUserName(user),
            CreatedAtUtc = now
        }).ToList();

        try
        {
            var updatedState = await _bankReconciliationService.ReplaceMatchesAsync(
                context.CompanyId,
                context.StateKey!,
                user,
                context.State.Matches.Concat(matches).ToList(),
                auditActionType: "confirm-manual-payment-bundle",
                expectedVersion: request.ExpectedVersion.Value,
                note: $"Manuell betalningsgrupp med {matches.Count} transaktioner mot faktura {invoice.InvoiceNo}.",
                cancellationToken: cancellationToken);

            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = true,
                Version = updatedState.Version,
                Matches = matches
            };
        }
        catch (BankReconciliationStateConflictException ex)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                Conflict = true,
                CurrentVersion = ex.CurrentVersion,
                ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _contextAccessor.HttpContext,
                    "BankReconciliationManualPaymentBundle conflict",
                    "Betalningsgruppen kunde inte sparas på grund av en versionskonflikt.",
                    ex)
            };
        }
        catch (BankReconciliationStateClosedException ex)
        {
            return new BankReconciliationPaymentBundleCommandResult
            {
                Success = false,
                CurrentVersion = ex.CurrentVersion,
                ErrorMessage = ex.Message
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual payment bundle confirmation failed for company {CompanyId}.", context.CompanyId);
            return Failure("Den manuella betalningsgruppen kunde inte sparas på grund av ett internt fel.");
        }
    }

    private async Task<BundleContext> LoadContextAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        CancellationToken cancellationToken)
    {
        var companyId = user?.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(source.StateKey))
            return BundleContext.Failed("Ingen aktiv avstämningskälla finns.");

        try
        {
            var invoiceResult = await _invoiceCandidateService.LoadAsync(
                source.IsDemoMode,
                user!,
                cancellationToken,
                demoScenarioKey: source.DemoScenarioKey);
            if (!string.IsNullOrWhiteSpace(invoiceResult.ErrorMessage))
                return BundleContext.Failed(invoiceResult.ErrorMessage);

            var state = await _bankReconciliationService.LoadStateAsync(companyId, source.StateKey, cancellationToken);
            var transactions = source.Transactions
                .Select(BankReconciliationTransactionPageService.MapTransactionCandidate)
                .ToList();

            return new BundleContext
            {
                Success = true,
                CompanyId = companyId,
                StateKey = source.StateKey,
                State = state,
                Transactions = transactions,
                Invoices = invoiceResult.Invoices
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment bundle context could not be loaded for company {CompanyId}.", companyId);
            return BundleContext.Failed("Betalningsgrupper kunde inte laddas på grund av ett internt fel.");
        }
    }

    private static string? BuildUserName(UserSession user)
    {
        var name = string.Join(" ", new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }

    private static bool IsCustomerPaymentCandidate(BankReconciliationTransactionCandidate transaction)
    {
        if (transaction.Amount <= 0m)
            return false;

        var typeKey = transaction.ResolvedCodingTypeKey?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(typeKey) || typeKey is "bankinbetalningar" or "def";
    }

    private static BankReconciliationPaymentBundleCommandResult Failure(string? message)
        => new()
        {
            Success = false,
            ErrorMessage = string.IsNullOrWhiteSpace(message)
                ? "Betalningsgruppen kunde inte behandlas."
                : message
        };

    private sealed class BundleContext
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public Guid CompanyId { get; init; }
        public string? StateKey { get; init; }
        public BankReconciliationPersistedState? State { get; init; }
        public IReadOnlyList<BankReconciliationTransactionCandidate>? Transactions { get; init; }
        public IReadOnlyList<WebApp.Models.Invoices.InvoiceItem>? Invoices { get; init; }

        public static BundleContext Failed(string? message)
            => new() { ErrorMessage = message };
    }
}
