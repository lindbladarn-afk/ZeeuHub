using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Input hashing gives AI requests traceability without logging raw bank or invoice data.
public static class BankReconciliationAiSuggestionInputHasher
{
    public static string BuildInputHash(BankReconciliationAiSuggestionRequest request)
    {
        var minimizedInput = new
        {
            request.CompanyId,
            transaction = new
            {
                request.Transaction.TransactionId,
                request.Transaction.Amount,
                request.Transaction.Currency,
                request.Transaction.EntryStatus,
                request.Transaction.Direction,
                request.Transaction.Date,
                request.Transaction.ValueDate,
                debtorName = NormalizeText(request.Transaction.DebtorName),
                remittance = NormalizeText(request.Transaction.Remittance),
                referenceCandidates = request.Transaction.ReferenceCandidates
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeReference)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            },
            candidates = request.RuleCandidates
                .Select(candidate => new
                {
                    invoiceId = candidate.Invoice.Id,
                    invoiceNo = candidate.Invoice.InvoiceNo,
                    customerName = NormalizeText(candidate.Invoice.CustomerName),
                    candidate.Invoice.RemainingAmount,
                    candidate.Invoice.Currency,
                    candidate.Invoice.IsSupplierInvoice,
                    candidate.Confidence.Score,
                    candidate.RuleKey,
                    candidate.RequiresManualConfirmation,
                    matchedNameTokens = candidate.Evidence.MatchedNameTokens
                        .Select(NormalizeText)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray()
                })
                .OrderBy(candidate => candidate.invoiceId, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        var json = JsonSerializer.Serialize(minimizedInput);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeReference(string value)
        => new string(value
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
