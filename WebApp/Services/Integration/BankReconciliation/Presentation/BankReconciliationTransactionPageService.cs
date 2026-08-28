using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation.Presentation;

// Builds bank reconciliation transaction pages from parsed transactions and invoice candidates.
public sealed class BankReconciliationTransactionPageService : IBankReconciliationTransactionPageService
{
    private readonly IBankReconciliationService _bankReconciliationService;

    public BankReconciliationTransactionPageService(IBankReconciliationService bankReconciliationService)
    {
        _bankReconciliationService = bankReconciliationService;
    }

    public BankReconciliationTransactionPageResult BuildPage(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        IReadOnlyList<InvoiceItem> invoices,
        int page,
        int pageSize,
        string? filter,
        string? groupFilter,
        string? classificationFilter)
    {
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var safePage = Math.Max(page, 1);
        var normalizedFilter = NormalizeFilter(filter, "all");
        var normalizedGroup = NormalizeFilter(groupFilter, "all");
        var normalizedClassification = NormalizeFilter(classificationFilter, "all");
        var resolutionStatuses = BuildResolutionStatuses(transactions, invoices);

        var filtered = transactions
            .Where(tx => MatchesClassificationFilter(tx, normalizedClassification))
            .Where(tx => normalizedGroup.Equals("all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tx.Group ?? "Ovrigt", normalizedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(tx => normalizedFilter switch
            {
                "matched" => resolutionStatuses[tx] == TransactionResolutionStatus.Matched,
                "partial" => GetAllocatedAmount(tx) > 0m && GetAllocatedAmount(tx) < GetMatchableAmount(tx),
                "review" => resolutionStatuses[tx] == TransactionResolutionStatus.Review,
                "unmatched" => resolutionStatuses[tx] == TransactionResolutionStatus.Unmatched,
                _ => true
            })
            .OrderByDescending(tx => ParseSortDate(tx.ValueDate ?? tx.Date))
            .ThenByDescending(tx => tx.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = filtered.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);

        return new BankReconciliationTransactionPageResult
        {
            Items = filtered
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(MapTransaction)
                .ToList(),
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Totals = BuildTransactionTotals(filtered),
            ClassificationSummary = BuildClassificationSummary(transactions),
            GroupCounts = BuildGroupCounts(transactions),
            ManualReviewItems = BuildManualReviewItems(filtered, invoices),
            AutoResultItems = BuildAutoResultItems(filtered),
            Summary = BuildSummary(resolutionStatuses)
        };
    }

    public BankReconciliationTransactionPageResult BuildEmptyPage(int page, int pageSize, string? errorMessage = null)
    {
        return new BankReconciliationTransactionPageResult
        {
            Page = Math.Max(page, 1),
            PageSize = Math.Clamp(pageSize, 1, 100),
            ErrorMessage = errorMessage
        };
    }

    private Dictionary<BankReconciliationParsedTransaction, TransactionResolutionStatus> BuildResolutionStatuses(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        IReadOnlyList<InvoiceItem> invoices)
    {
        var allocated = BuildAllocatedAmountsByInvoice(transactions);
        var result = new Dictionary<BankReconciliationParsedTransaction, TransactionResolutionStatus>();
        foreach (var tx in transactions)
        {
            if (GetMatchableAmount(tx) <= 0m || !IsInvoiceMatchGroup(tx.Group))
            {
                result[tx] = TransactionResolutionStatus.Excluded;
                continue;
            }

            if (IsFullyMatched(tx))
            {
                result[tx] = TransactionResolutionStatus.Matched;
                continue;
            }

            if (GetAllocatedAmount(tx) > 0m)
            {
                result[tx] = TransactionResolutionStatus.Review;
                continue;
            }

            var recommendations = _bankReconciliationService.BuildRecommendations(
                MapTransactionCandidate(tx),
                invoices,
                allocated);

            result[tx] = recommendations.Count > 0
                ? TransactionResolutionStatus.Review
                : TransactionResolutionStatus.Unmatched;
        }

        return result;
    }

    private static BankReconciliationSummary BuildSummary(
        IReadOnlyDictionary<BankReconciliationParsedTransaction, TransactionResolutionStatus> resolutionStatuses)
        => new()
        {
            Matched = resolutionStatuses.Values.Count(status => status == TransactionResolutionStatus.Matched),
            Review = resolutionStatuses.Values.Count(status => status == TransactionResolutionStatus.Review),
            Unmatched = resolutionStatuses.Values.Count(status => status == TransactionResolutionStatus.Unmatched)
        };

    private static bool IsFullyMatched(BankReconciliationParsedTransaction transaction)
        => GetAllocatedAmount(transaction) >= GetMatchableAmount(transaction);

    private enum TransactionResolutionStatus
    {
        Excluded,
        Matched,
        Review,
        Unmatched
    }

    private static BankReconciliationTransactionTotals BuildTransactionTotals(IReadOnlyList<BankReconciliationParsedTransaction> transactions)
    {
        var matchedTotal = transactions.Sum(tx => Math.Min(GetAllocatedAmount(tx), GetMatchableAmount(tx)));
        var matchableTotal = transactions.Where(tx => IsInvoiceMatchGroup(tx.Group)).Sum(GetMatchableAmount);

        return new BankReconciliationTransactionTotals
        {
            Credit = transactions.Where(tx => tx.Amount >= 0m).Sum(tx => tx.Amount),
            Debit = transactions.Where(tx => tx.Amount < 0m).Sum(tx => tx.Amount),
            Matched = matchedTotal,
            Unmatched = Math.Max(matchableTotal - matchedTotal, 0m)
        };
    }

    private static IReadOnlyList<BankReconciliationClassificationSummaryItem> BuildClassificationSummary(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions)
    {
        return transactions
            .GroupBy(tx => tx.Classification?.TypeKey ?? "def", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var classification = group.First().Classification;
                return new BankReconciliationClassificationSummaryItem
                {
                    Key = classification?.TypeKey ?? "def",
                    Label = classification?.TypeLabel ?? "DEF",
                    Count = group.Count(),
                    Amount = group.Sum(tx => Math.Abs(tx.Amount)),
                    DefaultCount = group.Count(tx => tx.Classification?.IsDefault ?? false),
                    RuleLabel = classification?.RuleLabel ?? "Standard",
                    SuggestedAccount = classification?.SuggestedAccount,
                    SuggestedCostCenter = classification?.SuggestedCostCenter,
                    IsDefault = classification?.IsDefault ?? true
                };
            })
            .OrderBy(item => item.IsDefault ? 0 : 1)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<BankReconciliationTransactionItem> BuildManualReviewItems(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        IReadOnlyList<InvoiceItem> invoices)
    {
        var allocated = BuildAllocatedAmountsByInvoice(transactions);

        return transactions
            .Where(tx => GetMatchableAmount(tx) > 0m && IsCustomerInvoiceTransaction(tx) && tx.Allocations.Count == 0)
            .Select(tx => new
            {
                Transaction = tx,
                Recommendations = _bankReconciliationService.BuildRecommendations(MapTransactionCandidate(tx), invoices, allocated)
            })
            .Where(item => item.Recommendations.Count == 0 || item.Recommendations[0].RequiresManualConfirmation)
            .Select(item => MapTransaction(item.Transaction))
            .ToList();
    }

    private static IReadOnlyList<BankReconciliationTransactionItem> BuildAutoResultItems(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions)
    {
        return transactions
            .Where(tx =>
                string.Equals(tx.MatchType, "auto", StringComparison.OrdinalIgnoreCase)
                || tx.Allocations.Any(allocation => string.Equals(allocation.MatchType, "auto", StringComparison.OrdinalIgnoreCase)))
            .Select(MapTransaction)
            .ToList();
    }

    private static BankReconciliationTransactionGroupCounts BuildGroupCounts(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions)
    {
        return new BankReconciliationTransactionGroupCounts
        {
            All = transactions.Count,
            Kundinbetalningar = transactions.Count(tx => string.Equals(tx.Group, "Kundinbetalningar", StringComparison.OrdinalIgnoreCase)),
            Leverantorsutbetalningar = transactions.Count(tx => string.Equals(tx.Group, "Leverantorsutbetalningar", StringComparison.OrdinalIgnoreCase)),
            Ovrigt = transactions.Count(tx => string.Equals(tx.Group ?? "Ovrigt", "Ovrigt", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static Dictionary<string, decimal> BuildAllocatedAmountsByInvoice(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions)
    {
        return transactions
            .SelectMany(tx => tx.Allocations)
            .GroupBy(allocation => allocation.InvoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.MatchedAmount),
                StringComparer.OrdinalIgnoreCase);
    }

    private static BankReconciliationTransactionItem MapTransaction(BankReconciliationParsedTransaction transaction)
    {
        return new BankReconciliationTransactionItem
        {
            Id = transaction.Id,
            Date = transaction.Date,
            ValueDate = transaction.ValueDate,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Reference = transaction.Reference,
            EndToEndId = transaction.EndToEndId,
            TxId = transaction.TxId,
            AcctSvcrRef = transaction.AcctSvcrRef,
            StatementId = transaction.StatementId,
            StatementAccountIban = transaction.StatementAccountIban,
            StatementAccountNumber = transaction.StatementAccountNumber,
            StatementAccountOwner = transaction.StatementAccountOwner,
            StatementBankBic = transaction.StatementBankBic,
            DebtorName = transaction.DebtorName,
            Remittance = transaction.Remittance,
            Direction = transaction.Direction,
            Domn = transaction.Domn,
            Fmly = transaction.Fmly,
            SubFmly = transaction.SubFmly,
            ScorType = transaction.ScorType,
            Group = transaction.Group,
            ClassificationRule = transaction.ClassificationRule,
            Classification = transaction.Classification,
            MatchedInvoiceId = transaction.MatchedInvoiceId,
            MatchType = transaction.MatchType,
            MatchRule = transaction.MatchRule,
            MatchedAmount = transaction.MatchedAmount,
            ReferenceCandidates = transaction.ReferenceCandidates.Select(candidate => new BankReconciliationReferenceCandidateItem
            {
                SourcePath = candidate.SourcePath,
                RawValue = candidate.RawValue,
                NormalizedValue = candidate.NormalizedValue,
                CandidateType = candidate.CandidateType
            }).ToList(),
            Allocations = transaction.Allocations.Select(allocation => new BankReconciliationAllocationItem
            {
                AllocationId = allocation.AllocationId,
                InvoiceId = allocation.InvoiceId,
                MatchType = allocation.MatchType,
                MatchRule = allocation.MatchRule,
                MatchedAmount = allocation.MatchedAmount,
                Currency = allocation.Currency
            }).ToList()
        };
    }

    public static BankReconciliationTransactionCandidate MapTransactionCandidate(BankReconciliationParsedTransaction transaction)
    {
        var classification = transaction.Classification;
        return new BankReconciliationTransactionCandidate
        {
            TransactionId = transaction.Id,
            Date = transaction.Date,
            ValueDate = transaction.ValueDate,
            EntryStatus = transaction.EntryStatus,
            Direction = transaction.Direction,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Reference = transaction.Reference,
            ReferenceType = transaction.ReferenceCandidates
                .FirstOrDefault(candidate => string.Equals(candidate.RawValue, transaction.Reference, StringComparison.OrdinalIgnoreCase))
                ?.CandidateType,
            EndToEndId = transaction.EndToEndId,
            TransactionIdSource = transaction.TxId,
            AccountServiceReference = transaction.AcctSvcrRef,
            StatementId = transaction.StatementId,
            StatementAccountIban = transaction.StatementAccountIban,
            StatementAccountNumber = transaction.StatementAccountNumber,
            StatementAccountOwner = transaction.StatementAccountOwner,
            StatementBankBic = transaction.StatementBankBic,
            DebtorName = transaction.DebtorName,
            Remittance = transaction.Remittance,
            ReferenceCandidates = transaction.ReferenceCandidates
                .Where(candidate => IsTrustedMatchingReferenceType(candidate.CandidateType))
                .Select(candidate => candidate.RawValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ResolvedCodingTypeKey = classification?.TypeKey,
            ResolvedCodingTypeLabel = classification?.TypeLabel,
            ResolvedCodingAccount = classification?.SuggestedAccount,
            ResolvedCodingCostCenter = classification?.SuggestedCostCenter,
            ResolvedCodingIsDefault = classification?.IsDefault ?? false
        };
    }

    private static bool IsTrustedMatchingReferenceType(string? candidateType)
        => candidateType is "creditor-reference"
            or "referred-document-number"
            or "unstructured-remittance"
            or "additional-remittance"
            or "end-to-end-id";

    private static DateTime ParseSortDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : DateTime.MinValue;

    private static decimal GetAllocatedAmount(BankReconciliationParsedTransaction transaction)
        => transaction.Allocations.Sum(allocation => allocation.MatchedAmount);

    private static decimal GetMatchableAmount(BankReconciliationParsedTransaction transaction)
        => Math.Abs(transaction.Amount);

    private static bool IsInvoiceMatchGroup(string? group)
        => string.Equals(group, "Kundinbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(group, "Leverantorsutbetalningar", StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomerInvoiceTransaction(BankReconciliationParsedTransaction transaction)
        => string.Equals(transaction.Group, "Kundinbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(transaction.Classification?.TypeKey, "bankinbetalningar", StringComparison.OrdinalIgnoreCase)
           || string.Equals(transaction.Classification?.TypeKey, "def", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesClassificationFilter(BankReconciliationParsedTransaction transaction, string normalizedClassification)
    {
        if (string.IsNullOrWhiteSpace(normalizedClassification) || normalizedClassification == "all")
            return true;

        return string.Equals(transaction.Classification?.TypeKey, normalizedClassification, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFilter(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
}
