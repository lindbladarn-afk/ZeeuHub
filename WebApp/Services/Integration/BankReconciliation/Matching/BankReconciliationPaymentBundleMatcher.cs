using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Builds bounded, deterministic payment bundles that always require human confirmation.
public sealed class BankReconciliationPaymentBundleMatcher : IBankReconciliationPaymentBundleMatcher
{
    private readonly IBankReconciliationMatchingService _matchingService;
    private readonly BankReconciliationPaymentBundleOptions _options;

    public BankReconciliationPaymentBundleMatcher(
        IBankReconciliationMatchingService matchingService,
        IOptions<BankReconciliationPaymentBundleOptions> options)
    {
        _matchingService = matchingService;
        _options = options.Value;
    }

    public IReadOnlyList<BankReconciliationPaymentBundleSuggestion> BuildSuggestions(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyList<BankReconciliationSavedMatch> existingMatches)
    {
        if (!_options.Enabled || transactions.Count < 2 || invoices.Count == 0)
            return Array.Empty<BankReconciliationPaymentBundleSuggestion>();

        var availableTransactions = BankReconciliationAllocationBalance
            .BuildAvailableTransactions(transactions, existingMatches)
            .Where(transaction => transaction.Amount > 0m)
            .ToList();
        var availableInvoices = BankReconciliationAllocationBalance.BuildAvailableInvoices(invoices, existingMatches);
        var suggestions = new List<BankReconciliationPaymentBundleSuggestion>();

        foreach (var invoice in availableInvoices)
        {
            var invoiceRemaining = invoice.RemainingAmount;

            var candidates = availableTransactions
                .Select(transaction => BuildCandidate(transaction, invoice))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate => candidate.EvidenceScore)
                .ThenBy(candidate => candidate.Transaction.TransactionId, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Clamp(_options.MaxCandidateTransactionsPerInvoice, 2, 20))
                .ToList();

            FindCombinations(invoice, invoiceRemaining, candidates, suggestions);
            if (suggestions.Count >= Math.Clamp(_options.MaxSuggestions, 1, 50))
                break;
        }

        return suggestions
            .OrderByDescending(suggestion => suggestion.ConfidenceScore)
            .ThenBy(suggestion => suggestion.AmountDifference)
            .ThenBy(suggestion => suggestion.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(_options.MaxSuggestions, 1, 50))
            .ToList();
    }

    private BundleCandidate? BuildCandidate(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice)
    {
        if (!string.IsNullOrWhiteSpace(transaction.Currency) &&
            !string.Equals(transaction.Currency, "SEK", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var recommendation = _matchingService
            .BuildRecommendations(transaction, new[] { invoice }, new Dictionary<string, decimal>(), maxResults: 1)
            .SingleOrDefault();
        if (recommendation is null ||
            recommendation.Confidence.Score < Math.Clamp(_options.MinimumTransactionEvidenceScore, 0, 100) ||
            !recommendation.RuleKey.Contains("ref-exact", StringComparison.Ordinal))
        {
            return null;
        }

        return new BundleCandidate(transaction, recommendation.Confidence.Score, recommendation.RuleKey);
    }

    private void FindCombinations(
        InvoiceItem invoice,
        decimal invoiceRemaining,
        IReadOnlyList<BundleCandidate> candidates,
        ICollection<BankReconciliationPaymentBundleSuggestion> suggestions)
    {
        var maxBundleSize = Math.Clamp(_options.MaxTransactionsPerBundle, 2, 12);
        var maxSuggestions = Math.Clamp(_options.MaxSuggestions, 1, 50);
        var current = new List<BundleCandidate>();

        void Search(int index, decimal total)
        {
            if (suggestions.Count >= maxSuggestions || total > invoiceRemaining + _options.AmountTolerance)
                return;

            if (current.Count >= 2 && Math.Abs(invoiceRemaining - total) <= _options.AmountTolerance)
            {
                suggestions.Add(CreateSuggestion(invoice, invoiceRemaining, current, total));
                return;
            }

            if (index >= candidates.Count || current.Count >= maxBundleSize)
                return;

            for (var candidateIndex = index; candidateIndex < candidates.Count; candidateIndex++)
            {
                current.Add(candidates[candidateIndex]);
                Search(candidateIndex + 1, total + candidates[candidateIndex].Transaction.Amount);
                current.RemoveAt(current.Count - 1);
            }
        }

        Search(0, 0m);
    }

    private static BankReconciliationPaymentBundleSuggestion CreateSuggestion(
        InvoiceItem invoice,
        decimal invoiceRemaining,
        IReadOnlyList<BundleCandidate> candidates,
        decimal total)
    {
        var allocations = candidates
            .OrderBy(candidate => candidate.Transaction.TransactionId, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new BankReconciliationPaymentBundleAllocation
            {
                TransactionId = candidate.Transaction.TransactionId,
                Date = candidate.Transaction.ValueDate ?? candidate.Transaction.Date,
                DebtorName = candidate.Transaction.DebtorName,
                Reference = candidate.Transaction.Reference,
                Remittance = candidate.Transaction.Remittance,
                MatchedAmount = candidate.Transaction.Amount,
                Currency = string.IsNullOrWhiteSpace(candidate.Transaction.Currency) ? "SEK" : candidate.Transaction.Currency,
                EvidenceScore = candidate.EvidenceScore,
                RuleKey = candidate.RuleKey,
                ExactReferenceMatched = candidate.RuleKey.Contains("ref-exact", StringComparison.Ordinal)
            })
            .ToList();
        var amountDifference = Math.Abs(invoiceRemaining - total);

        return new BankReconciliationPaymentBundleSuggestion
        {
            BundleId = BuildBundleId(invoice.InvoiceNo, allocations),
            InvoiceId = invoice.InvoiceNo,
            InvoiceNo = invoice.InvoiceNo,
            InvoiceOcr = string.IsNullOrWhiteSpace(invoice.Ocr) ? null : invoice.Ocr,
            CustomerName = invoice.Customer,
            InvoiceDueDate = invoice.DueDate == default ? null : invoice.DueDate.ToString("yyyy-MM-dd"),
            InvoiceRemainingAmount = invoiceRemaining,
            TotalMatchedAmount = total,
            AmountDifference = amountDifference,
            Currency = "SEK",
            ConfidenceScore = Math.Min(100, (int)Math.Round(allocations.Average(item => item.EvidenceScore) + 10d)),
            ReasonCode = amountDifference == 0m ? "exact-sum+exact-reference" : "tolerance-sum+exact-reference",
            Explanation = $"{allocations.Count} betalningar har fakturareferens och summerar till fakturans kvarvarande belopp.",
            RequiresManualConfirmation = true,
            Allocations = allocations
        };
    }

    private static string BuildBundleId(
        string invoiceId,
        IReadOnlyList<BankReconciliationPaymentBundleAllocation> allocations)
    {
        var canonical = string.Join("|", new[] { invoiceId.Trim().ToUpperInvariant() }
            .Concat(allocations.Select(item => string.Concat(
                item.TransactionId.Trim().ToUpperInvariant(),
                ":",
                item.MatchedAmount.ToString("0.00", CultureInfo.InvariantCulture)))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
    }

    private sealed record BundleCandidate(
        BankReconciliationTransactionCandidate Transaction,
        int EvidenceScore,
        string RuleKey);
}
