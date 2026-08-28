using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// AI suggestion verifier prevents suggestions from introducing invoices, amounts, or currencies outside rule evidence.
public sealed class BankReconciliationAiSuggestionVerifier : IBankReconciliationAiSuggestionVerifier
{
    private readonly IBankReconciliationMatchEligibilityService _eligibilityService;

    public BankReconciliationAiSuggestionVerifier(
        IBankReconciliationMatchEligibilityService? eligibilityService = null)
    {
        _eligibilityService = eligibilityService ?? new BankReconciliationMatchEligibilityService();
    }

    public BankReconciliationMatchEligibilityResult EvaluateEligibility(
        BankReconciliationAiSuggestionRequest request,
        BankReconciliationRecommendationItem ruleCandidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ruleCandidate);

        var invoice = ruleCandidate.Invoice;
        return _eligibilityService.Evaluate(request.Transaction, new InvoiceItem
        {
            InvoiceNo = invoice.InvoiceNo,
            Ocr = invoice.Ocr ?? string.Empty,
            Customer = invoice.CustomerName ?? string.Empty,
            AmountSek = invoice.Amount,
            RemainingAmount = invoice.RemainingAmount,
            Currency = invoice.Currency,
            IsSupplierInvoice = invoice.IsSupplierInvoice,
            DueDate = DateTime.TryParse(invoice.DueDate, out var dueDate) ? dueDate : DateTime.Today
        });
    }

    public BankReconciliationAiSuggestionVerificationResult Verify(
        BankReconciliationAiSuggestionRequest request,
        BankReconciliationAiSuggestionCandidate candidate)
    {
        var result = new BankReconciliationAiSuggestionVerificationResult
        {
            Candidate = candidate
        };

        if (request.CompanyId == Guid.Empty)
        {
            result.Errors.Add("CompanyId saknas.");
        }

        if (string.IsNullOrWhiteSpace(request.StateKey))
        {
            result.Errors.Add("Avstämningskälla saknas.");
        }

        if (string.IsNullOrWhiteSpace(request.Transaction.TransactionId))
        {
            result.Errors.Add("Transaktion saknas.");
        }

        var ruleCandidate = request.RuleCandidates.FirstOrDefault(item =>
            string.Equals(item.Invoice.Id, candidate.InvoiceId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Invoice.InvoiceNo, candidate.InvoiceId, StringComparison.OrdinalIgnoreCase));

        if (ruleCandidate is null)
        {
            result.Errors.Add("AI-förslaget avser en faktura som inte finns bland regelmotorns kandidater.");
        }

        if (candidate.MatchedAmount <= 0m)
        {
            result.Errors.Add("Matchbelopp måste vara större än noll.");
        }

        if (ruleCandidate is not null)
        {
            var eligibility = EvaluateEligibility(request, ruleCandidate);
            foreach (var blockedRule in eligibility.Rules.Where(rule =>
                         string.Equals(rule.Status, "blocked", StringComparison.Ordinal)))
            {
                result.Errors.Add($"AI-förslaget blockerades av matchningsregeln: {blockedRule.Message}");
            }

            var maxAllowedAmount = Math.Min(
                Math.Abs(request.Transaction.Amount),
                Math.Max(ruleCandidate.Invoice.RemainingAmount, 0m));

            if (candidate.MatchedAmount > maxAllowedAmount)
            {
                result.Errors.Add("Matchbeloppet överstiger transaktionens eller fakturans kvarvarande belopp.");
            }

            if (!string.Equals(candidate.Currency, ruleCandidate.Invoice.Currency, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("Valutan matchar inte fakturakandidaten.");
            }

            if (ruleCandidate.RequiresManualConfirmation && candidate.RequiresManualConfirmation == false)
            {
                result.Errors.Add("AI får inte ta bort krav på manuell bekräftelse från regelmotorn.");
            }
        }

        if (candidate.RequiresManualConfirmation == false)
        {
            result.Errors.Add("AI-assisterade förslag måste alltid kräva manuell bekräftelse.");
        }

        if (candidate.ConfidenceScore is < 0 or > 100)
        {
            result.Errors.Add("ConfidenceScore måste vara mellan 0 och 100.");
        }

        candidate.VerificationStatus = result.Errors.Count == 0 ? "verified" : "rejected";
        candidate.VerificationErrors = result.Errors.ToList();
        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}
