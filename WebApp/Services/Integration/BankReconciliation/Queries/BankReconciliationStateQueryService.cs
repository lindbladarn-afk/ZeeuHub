using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Projects persisted bank reconciliation state into the UI state contract.
public sealed class BankReconciliationStateQueryService : IBankReconciliationStateQueryService
{
    private readonly IBankReconciliationService _bankReconciliationService;

    public BankReconciliationStateQueryService(IBankReconciliationService bankReconciliationService)
    {
        _bankReconciliationService = bankReconciliationService;
    }

    public async Task<BankReconciliationStateQueryResult> BuildStateAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        CancellationToken cancellationToken)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty || string.IsNullOrWhiteSpace(source.StateKey))
        {
            return new BankReconciliationStateQueryResult();
        }

        var state = await _bankReconciliationService.LoadStateAsync(
            companyId,
            source.StateKey,
            cancellationToken);

        return new BankReconciliationStateQueryResult
        {
            Success = true,
            Version = state.Version,
            MatchCount = state.Matches.Count,
            IsClosed = state.IsClosed,
            ClosedAtUtc = state.ClosedAtUtc,
            ClosedByName = state.ClosedByName,
            Matches = state.Matches.Select(MapMatch).ToList(),
            RecentActivity = state.AuditTrail
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(8)
                .Select(MapActivity)
                .ToList()
        };
    }

    private static BankReconciliationStateMatchItem MapMatch(BankReconciliationSavedMatch match)
        => new()
        {
            AllocationId = match.AllocationId,
            TransactionId = match.TransactionId,
            InvoiceId = match.InvoiceId,
            MatchType = match.MatchType,
            MatchRule = match.MatchRule,
            MatchedAmount = match.MatchedAmount,
            Currency = match.Currency
        };

    private static BankReconciliationStateActivityItem MapActivity(BankReconciliationAuditEntry activity)
        => new()
        {
            CreatedAtUtc = activity.CreatedAtUtc,
            ActionType = activity.ActionType,
            UserName = activity.UserName,
            TransactionId = activity.TransactionId,
            InvoiceId = activity.InvoiceId,
            MatchType = activity.MatchType,
            MatchRule = activity.MatchRule,
            MatchedAmount = activity.MatchedAmount,
            Note = activity.Note
        };
}
