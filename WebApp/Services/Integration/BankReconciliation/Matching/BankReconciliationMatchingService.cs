using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Rule-based bank reconciliation matching scores explicit transaction and invoice signals.
public sealed class BankReconciliationMatchingService : IBankReconciliationMatchingService
{
    private readonly BankReconciliationMatchingOptions _options;
    private readonly IBankReconciliationMatchEligibilityService _eligibilityService;

    public BankReconciliationMatchingService(
        IOptions<BankReconciliationMatchingOptions>? options = null,
        IBankReconciliationMatchEligibilityService? eligibilityService = null)
    {
        _options = options?.Value ?? new BankReconciliationMatchingOptions();
        _eligibilityService = eligibilityService ?? new BankReconciliationMatchEligibilityService();
    }

    public BankReconciliationMatchEligibilityResult EvaluateEligibility(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice)
        => _eligibilityService.Evaluate(transaction, invoice);

    public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
        BankReconciliationTransactionCandidate transaction,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
        int maxResults = 4)
    {
        if (!CanUseInvoiceMatching(transaction))
            return Array.Empty<BankReconciliationRecommendationItem>();

        var take = maxResults > 0 ? maxResults : _options.RecommendationMaxResults;
        return invoices
            .Select(invoice => CreateRecommendation(transaction, invoice, allocatedAmountsByInvoiceId))
            .Where(item => item is not null)
            .Where(item => item!.Confidence.Score >= _options.RecommendationMinimumScore)
            .OrderByDescending(item => item!.Confidence.Score)
            .ThenBy(item => item!.Invoice.DueDate)
            .Take(take)
            .Select(item => item!)
            .ToList();
    }

    public BankReconciliationAutoMatchResult BuildAutoMatches(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices)
    {
        var result = new BankReconciliationAutoMatchResult();
        var allocatedAmounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var candidates = transactions.Where(IsInvoiceMatchCandidate).ToList();

        TryMatch(candidates, invoices, allocatedAmounts, result.Matches,
            recommendation => recommendation.Confidence.Score >= _options.AutoMatchReferenceAmountScore &&
                              recommendation.RuleKey.Contains("ref-exact", StringComparison.Ordinal) &&
                              recommendation.RuleKey.Contains("amount-exact", StringComparison.Ordinal),
            "reference+amount");

        TryMatch(candidates, invoices, allocatedAmounts, result.Matches,
            recommendation => recommendation.Confidence.Score >= _options.AutoMatchReferenceScore &&
                              recommendation.RuleKey.Contains("ref-exact", StringComparison.Ordinal),
            "reference");

        TryMatch(candidates, invoices, allocatedAmounts, result.Matches,
            recommendation => recommendation.Confidence.Score >= _options.AutoMatchAmountNameScore &&
                              recommendation.RuleKey.Contains("amount-exact", StringComparison.Ordinal) &&
                              recommendation.RuleKey.Contains("name", StringComparison.Ordinal),
            "amount+name");

        TryMatch(candidates, invoices, allocatedAmounts, result.Matches,
            recommendation => recommendation.Confidence.Score >= _options.AutoMatchAmountDateScore &&
                              recommendation.RuleKey.Contains("amount-exact", StringComparison.Ordinal) &&
                              recommendation.RuleKey.Contains("date", StringComparison.Ordinal),
            "amount+date");

        return result;
    }

    private void TryMatch(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices,
        Dictionary<string, decimal> allocatedAmountsByInvoiceId,
        ICollection<BankReconciliationSavedMatch> matches,
        Func<BankReconciliationRecommendationItem, bool> predicate,
        string matchRule)
    {
        foreach (var transaction in transactions)
        {
            if (matches.Any(x => string.Equals(x.TransactionId, transaction.TransactionId, StringComparison.OrdinalIgnoreCase)))
                continue;

            var matchingCandidates = invoices
                .Select(invoice => CreateRecommendation(transaction, invoice, allocatedAmountsByInvoiceId))
                .Where(item => item is not null)
                .Where(item => predicate(item!))
                .Where(item => item!.RequiresManualConfirmation == false)
                .OrderByDescending(item => item!.Confidence.Score)
                .ThenBy(item => item!.Invoice.DueDate)
                .Take(2)
                .Select(item => item!)
                .ToList();

            if (matchingCandidates.Count() != 1)
                continue;

            var chosen = matchingCandidates[0];
            var matchedAmount = GetMatchableAmount(transaction);
            allocatedAmountsByInvoiceId[chosen.Invoice.Id] =
                (allocatedAmountsByInvoiceId.TryGetValue(chosen.Invoice.Id, out var existing) ? existing : 0m) + matchedAmount;

            matches.Add(new BankReconciliationSavedMatch
            {
                TransactionId = transaction.TransactionId,
                InvoiceId = chosen.Invoice.Id,
                MatchType = "auto",
                MatchRule = matchRule,
                MatchedAmount = matchedAmount,
                Currency = string.IsNullOrWhiteSpace(transaction.Currency) ? "SEK" : transaction.Currency,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
    }

    private static bool IsInvoiceMatchCandidate(BankReconciliationTransactionCandidate transaction)
        => GetMatchableAmount(transaction) > 0m && CanUseInvoiceMatching(transaction);

    private static bool CanUseInvoiceMatching(BankReconciliationTransactionCandidate transaction)
    {
        if (GetMatchableAmount(transaction) <= 0m)
            return false;

        var typeKey = NormalizeTypeKey(transaction.ResolvedCodingTypeKey);
        if (string.IsNullOrWhiteSpace(typeKey))
            return true;

        return typeKey is "bankinbetalningar" or "leverantorsbetalning" or "def";
    }

    private static string NormalizeTypeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static decimal GetMatchableAmount(BankReconciliationTransactionCandidate transaction)
        => Math.Abs(transaction.Amount);

    private BankReconciliationRecommendationItem? CreateRecommendation(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice,
        IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId)
    {
        var eligibility = EvaluateEligibility(transaction, invoice);
        if (!eligibility.IsEligible)
            return null;

        var allocated = allocatedAmountsByInvoiceId.TryGetValue(invoice.InvoiceNo, out var amount) ? amount : 0m;
        var remaining = Math.Max(invoice.RemainingAmount - allocated, 0m);
        var transactionAmount = GetMatchableAmount(transaction);
        if (remaining < transactionAmount)
            return null;

        var evaluation = EvaluateMatch(transaction, invoice, remaining, _options);
        var signals = evaluation.Signals;
        var confidence = GetConfidence(signals);

        var recommendation = new BankReconciliationRecommendationItem
        {
            Invoice = new BankReconciliationRecommendationInvoice
            {
                Id = invoice.InvoiceNo,
                InvoiceNo = invoice.InvoiceNo,
                Ocr = string.IsNullOrWhiteSpace(invoice.Ocr) ? null : invoice.Ocr,
                CustomerName = invoice.Customer,
                Amount = invoice.AmountSek,
                RemainingAmount = remaining,
                Currency = invoice.Currency,
                DueDate = invoice.DueDate.ToString("yyyy-MM-dd"),
                IsSupplierInvoice = invoice.IsSupplierInvoice
            },
            Confidence = confidence,
            RuleLabel = GetRuleLabel(signals),
            RuleHelp = GetRuleHelp(signals),
            RuleKey = BuildRuleKey(signals),
            RequiresManualConfirmation = eligibility.RequiresManualReview || RequiresManualConfirmation(signals, confidence),
            ManualConfirmationReason = eligibility.RequiresManualReview
                ? eligibility.Rules.First(rule => string.Equals(rule.Status, "warning", StringComparison.Ordinal)).Message
                : GetManualConfirmationReason(signals, confidence),
            Evidence = evaluation.Evidence
        };

        recommendation.Evidence.EligibilityRules = eligibility.Rules;
        return recommendation;
    }

    private BankReconciliationMatchEvaluation EvaluateMatch(
        BankReconciliationTransactionCandidate tx,
        InvoiceItem invoice,
        decimal remainingAmount,
        BankReconciliationMatchingOptions options)
    {
        var txRefs = BuildTransactionReferences(tx);
        var invRefs = BuildInvoiceReferences(invoice);

        var referenceMatches = txRefs
            .SelectMany(txRef => invRefs
                .Where(invRef => txRef.NormalizedValue == invRef.NormalizedValue)
                .Select(invRef => BuildReferenceEvidence(txRef, invRef, "exact")))
            .ToList();
        var refExact = referenceMatches.Count > 0;

        if (!refExact)
        {
            referenceMatches = txRefs
                .SelectMany(txRef => invRefs
                    .Where(invRef => invRef.NormalizedValue.Length >= options.MinimumPartialReferenceLength
                        && txRef.NormalizedValue.Length > invRef.NormalizedValue.Length
                        && txRef.NormalizedValue.Contains(invRef.NormalizedValue, StringComparison.Ordinal))
                    .Select(invRef => BuildReferenceEvidence(txRef, invRef, "partial")))
                .ToList();
        }

        var refPartial = !refExact && referenceMatches.Count > 0;
        var transactionAmount = GetMatchableAmount(tx);
        var amountDifferenceToRemaining = Math.Abs(transactionAmount - remainingAmount);
        var amountDifferenceToInvoice = Math.Abs(transactionAmount - invoice.AmountSek);
        var amountDifference = Math.Min(amountDifferenceToRemaining, amountDifferenceToInvoice);
        var amountExact = amountDifference == 0m;
        var amountTolerance = !amountExact && amountDifference <= options.AmountTolerance;
        var currencyMatch = string.Equals(tx.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase);

        var txTokens = TokenizeName(tx.DebtorName);
        var invTokens = TokenizeName(invoice.Customer);
        var matchedNameTokens = txTokens
            .Where(token => invTokens.Contains(token, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var nameMatch = matchedNameTokens.Count > 0;

        var txDate = ParseDate(tx.Date ?? tx.ValueDate);
        var dateDifferenceDays = txDate.HasValue
            ? Math.Abs((txDate.Value.Date - invoice.DueDate.Date).Days)
            : (int?)null;
        var dateMatch = dateDifferenceDays.HasValue && dateDifferenceDays.Value <= options.DateWindowDays;

        return new BankReconciliationMatchEvaluation
        {
            Signals = new BankReconciliationMatchSignals
            {
                RefExact = refExact,
                RefPartial = refPartial,
                AmountExact = amountExact,
                AmountTolerance = amountTolerance,
                CurrencyMatch = currencyMatch,
                NameMatch = nameMatch,
                DateMatch = dateMatch
            },
            Evidence = new BankReconciliationMatchEvidence
            {
                ReferenceMatches = referenceMatches,
                TransactionAmount = transactionAmount,
                InvoiceRemainingAmount = remainingAmount,
                InvoiceAmount = invoice.AmountSek,
                AmountDifference = amountDifference,
                Currency = string.IsNullOrWhiteSpace(tx.Currency) ? "SEK" : tx.Currency,
                CurrencyMatched = currencyMatch,
                MatchedNameTokens = matchedNameTokens,
                DateDifferenceDays = dateDifferenceDays
            }
        };
    }

    private List<BankReconciliationNormalizedReference> BuildTransactionReferences(BankReconciliationTransactionCandidate tx)
    {
        return new[]
            {
                new BankReconciliationReferenceInput("reference", IsTrustedMatchingReferenceType(tx.ReferenceType) ? tx.Reference : null),
                new BankReconciliationReferenceInput("remittance", tx.Remittance),
                new BankReconciliationReferenceInput("end-to-end-id", tx.EndToEndId)
            }
            .Concat(tx.ReferenceCandidates.Select(value => new BankReconciliationReferenceInput("reference-candidate", value)))
            .Select(input => new BankReconciliationNormalizedReference(input.Source, input.Value ?? string.Empty, NormalizeRef(input.Value)))
            .Where(value => value.NormalizedValue.Length >= _options.MinimumExactReferenceLength)
            .Where(value => !IsPlaceholderReference(value.NormalizedValue))
            .DistinctBy(value => $"{value.Source}:{value.NormalizedValue}")
            .ToList();
    }

    private List<BankReconciliationNormalizedReference> BuildInvoiceReferences(InvoiceItem invoice)
    {
        return new[]
            {
                new BankReconciliationReferenceInput("ocr", invoice.Ocr),
                new BankReconciliationReferenceInput("invoice-number", invoice.InvoiceNo)
            }
            .Select(input => new BankReconciliationNormalizedReference(input.Source, input.Value ?? string.Empty, NormalizeRef(input.Value)))
            .Where(value => value.NormalizedValue.Length >= _options.MinimumExactReferenceLength)
            .Where(value => !IsPlaceholderReference(value.NormalizedValue))
            .DistinctBy(value => $"{value.Source}:{value.NormalizedValue}")
            .ToList();
    }

    private static BankReconciliationReferenceEvidence BuildReferenceEvidence(
        BankReconciliationNormalizedReference transactionReference,
        BankReconciliationNormalizedReference invoiceReference,
        string matchType)
        => new()
        {
            TransactionSource = transactionReference.Source,
            TransactionValue = transactionReference.RawValue,
            InvoiceSource = invoiceReference.Source,
            InvoiceValue = invoiceReference.RawValue,
            NormalizedTransactionValue = transactionReference.NormalizedValue,
            NormalizedInvoiceValue = invoiceReference.NormalizedValue,
            MatchType = matchType
        };

    private static BankReconciliationConfidence GetConfidence(BankReconciliationMatchSignals signals)
    {
        var score = 0;
        if (signals.RefExact) score += 60;
        if (signals.RefPartial) score += 30;
        if (signals.AmountExact) score += 30;
        if (signals.AmountTolerance) score += 15;
        if (signals.NameMatch) score += 10;
        if (signals.DateMatch) score += 10;
        if (signals.CurrencyMatch) score += 5;

        return new BankReconciliationConfidence
        {
            Level = score >= 80 ? "Hög" : score >= 50 ? "Medel" : "Låg",
            Score = score
        };
    }

    private static string GetRuleLabel(BankReconciliationMatchSignals signals)
    {
        if (signals.RefExact && signals.AmountExact) return "OCR/Referens + Belopp";
        if (signals.RefExact && signals.NameMatch) return "OCR/Referens + Betalare";
        if (signals.AmountExact && signals.NameMatch) return "Belopp + Betalare";
        if (signals.AmountExact && signals.DateMatch) return "Belopp + Datum";
        if (signals.RefExact) return "OCR/Referens";
        if (signals.AmountExact) return "Belopp";
        if (signals.NameMatch) return "Betalare";
        if (signals.DateMatch) return "Datum";
        return "Manuell";
    }

    private static string GetRuleHelp(BankReconciliationMatchSignals signals)
    {
        if (signals.RefExact && signals.AmountExact) return "OCR/Referens och belopp matchade fakturan.";
        if (signals.RefExact && signals.NameMatch) return "OCR/Referens och betalarnamn matchade fakturan.";
        if (signals.AmountExact && signals.NameMatch) return "Belopp och betalarnamn matchade fakturan.";
        if (signals.AmountExact && signals.DateMatch) return "Belopp matchade fakturan och datum låg nära förfallodatum.";
        if (signals.RefExact) return "OCR/Referens matchade fakturan.";
        if (signals.AmountExact) return "Belopp matchade fakturan exakt.";
        if (signals.NameMatch) return "Betalarnamn matchade kund.";
        if (signals.DateMatch) return "Datum låg nära förfallodatum.";
        return "Matchningen kräver manuell kontroll.";
    }

    private static string BuildRuleKey(BankReconciliationMatchSignals signals)
    {
        var parts = new List<string>();
        if (signals.RefExact) parts.Add("ref-exact");
        if (signals.RefPartial) parts.Add("ref-partial");
        if (signals.AmountExact) parts.Add("amount-exact");
        if (signals.AmountTolerance) parts.Add("amount-tolerance");
        if (signals.NameMatch) parts.Add("name");
        if (signals.DateMatch) parts.Add("date");
        if (signals.CurrencyMatch) parts.Add("currency");
        return string.Join("|", parts);
    }

    private bool RequiresManualConfirmation(BankReconciliationMatchSignals signals, BankReconciliationConfidence confidence)
    {
        if (confidence.Score < _options.ManualConfirmationMinimumScore)
            return true;

        if (signals.AmountTolerance)
            return true;

        if (signals.RefPartial)
            return true;

        if (!signals.RefExact && (signals.NameMatch || signals.DateMatch))
            return true;

        return false;
    }

    private string? GetManualConfirmationReason(BankReconciliationMatchSignals signals, BankReconciliationConfidence confidence)
    {
        if (!RequiresManualConfirmation(signals, confidence))
            return null;

        if (signals.AmountTolerance)
            return "Beloppet ligger inom tolerans men är inte exakt.";

        if (signals.RefPartial)
            return "Referensen är bara en delträff och bör granskas manuellt.";

        if (!signals.RefExact && signals.NameMatch)
            return "Matchningen bygger på namn och behöver manuell bekräftelse.";

        if (!signals.RefExact && signals.DateMatch)
            return "Matchningen bygger på datum och behöver manuell bekräftelse.";

        return "Matchningen är inte tillräckligt stark för att köras automatiskt.";
    }

    private static string NormalizeRef(string? value)
    {
        return Regex.Replace((value ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]", string.Empty).Trim();
    }

    private static bool IsPlaceholderReference(string normalizedValue)
        => normalizedValue is "NOTPROVIDED" or "NOTAVAILABLE" or "UNKNOWN" or "NONE" or "NULL" or "NA";

    private static bool IsTrustedMatchingReferenceType(string? referenceType)
        => string.IsNullOrWhiteSpace(referenceType)
            || referenceType is "creditor-reference"
                or "referred-document-number"
                or "unstructured-remittance"
                or "additional-remittance"
                or "end-to-end-id";

    private static string[] TokenizeName(string? value)
    {
        var normalized = NormalizeName(value);
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length >= 3)
            .ToArray();
    }

    private static string NormalizeName(string? value)
    {
        var upper = (value ?? string.Empty).ToUpperInvariant();
        upper = Regex.Replace(upper, @"\s+", " ");
        upper = Regex.Replace(upper, @"[^A-ZÅÄÖ0-9 ]", string.Empty);
        return upper.Trim();
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date
            : null;
    }

    private sealed record BankReconciliationMatchEvaluation
    {
        public BankReconciliationMatchSignals Signals { get; init; } = new();
        public BankReconciliationMatchEvidence Evidence { get; init; } = new();
    }

    private sealed record BankReconciliationReferenceInput(string Source, string? Value);

    private sealed record BankReconciliationNormalizedReference(string Source, string RawValue, string NormalizedValue);
}
