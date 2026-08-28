using System.Globalization;
using System.Text;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Classifier maps CAMT signals to a stable reconciliation type without changing the legacy UI grouping.
public static class BankReconciliationTransactionClassifier
{
    public static BankReconciliationTransactionClassification Classify(
        string? domn,
        string? fmly,
        string? subFmly,
        string? direction,
        string? scorType,
        string? remittance = null,
        string? debtorName = null)
    {
        var normalizedText = BuildNormalizedText(remittance, debtorName);
        var isInternalTransfer = IsMatch(domn, "TRAD") && IsMatch(fmly, "NTAV");
        var isCustomerInpayment = IsMatch(domn, "PMNT") && IsMatch(fmly, "RCDT");
        var isSupplierPayment = IsMatch(domn, "PMNT") && IsMatch(fmly, "ICDT") && IsMatch(direction, "DBIT");
        var isAutogiro = IsMatch(domn, "PMNT") && IsMatch(fmly, "RDDT");
        var isCashWithdrawal = IsMatch(domn, "PMNT") && IsMatch(fmly, "CNTR");
        var hasInterestSignal = ContainsAny(normalizedText, "RANTA", "RANTE", "RANTEKONTO", "INTEREST");
        var hasFeeSignal = ContainsAny(normalizedText, "AVGIFT", "FEE", "SERVICEAVGIFT", "KORTAVGIFT");
        var hasTaxSignal = ContainsAny(normalizedText, "SKATTEVERKET", "SKATT", "MOMS", "TAX");

        if (isInternalTransfer)
        {
            return Create(
                typeKey: "overforing-konto",
                typeLabel: "Överföring konto",
                ruleKey: "trad+ntav",
                ruleLabel: "TRAD/NTAV",
                suggestedAccount: "1930",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: "trad+ntav");
        }

        if (hasInterestSignal || IsMatch(domn, "INTC") || IsMatch(fmly, "INT"))
        {
            return Create(
                typeKey: "rantekonto",
                typeLabel: "Räntekonto",
                ruleKey: hasInterestSignal ? "text:ranta" : "domn+fmly",
                ruleLabel: hasInterestSignal ? "Text match: ränta" : $"{NormalizeRulePart(domn)}/{NormalizeRulePart(fmly)}",
                suggestedAccount: "8310",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: hasInterestSignal ? "text:ranta" : "domn+fmly");
        }

        if (hasFeeSignal)
        {
            return Create(
                typeKey: "bankavgift",
                typeLabel: "Bankavgift",
                ruleKey: "text:avgift",
                ruleLabel: "Text match: avgift",
                suggestedAccount: "6570",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: "text:avgift");
        }

        if (hasTaxSignal)
        {
            return Create(
                typeKey: "skattebetalning",
                typeLabel: "Skattebetalning",
                ruleKey: "text:skatt",
                ruleLabel: "Text match: skatt",
                suggestedAccount: "1630",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: "text:skatt");
        }

        if (isAutogiro)
        {
            return Create(
                typeKey: "autogiro",
                typeLabel: "Autogiro",
                ruleKey: "pmnt+rddt",
                ruleLabel: "PMNT/RDDT",
                suggestedAccount: "2440",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: "pmnt+rddt");
        }

        if (isCashWithdrawal)
        {
            return Create(
                typeKey: "kontantuttag",
                typeLabel: "Kontantuttag",
                ruleKey: "pmnt+cntr",
                ruleLabel: "PMNT/CNTR",
                suggestedAccount: "1910",
                suggestedCostCenter: null,
                legacyGroup: "Ovrigt",
                legacyRule: "pmnt+cntr");
        }

        if (isSupplierPayment)
        {
            return Create(
                typeKey: "leverantorsbetalning",
                typeLabel: "Leverantörsbetalning",
                ruleKey: "domn+fmly+dbit",
                ruleLabel: "PMNT/ICDT/DBIT",
                suggestedAccount: "2440",
                suggestedCostCenter: null,
                legacyGroup: "Leverantorsutbetalningar",
                legacyRule: "domn+fmly+dbit");
        }

        if (isCustomerInpayment || IsMatch(scorType, "SCOR") && IsMatch(direction, "CRDT"))
        {
            var isScorFallback = !isCustomerInpayment && IsMatch(scorType, "SCOR") && IsMatch(direction, "CRDT");
            return Create(
                typeKey: "bankinbetalningar",
                typeLabel: "Bankinbetalningar",
                ruleKey: isScorFallback ? "scor+crdt" : "domn+fmly",
                ruleLabel: isScorFallback ? "SCOR/CRDT" : "PMNT/RCDT",
                suggestedAccount: "1510",
                suggestedCostCenter: null,
                legacyGroup: "Kundinbetalningar",
                legacyRule: isScorFallback ? "scor+crdt" : "domn+fmly");
        }

        return CreateDefault();
    }

    private static BankReconciliationTransactionClassification Create(
        string typeKey,
        string typeLabel,
        string ruleKey,
        string ruleLabel,
        string? suggestedAccount,
        string? suggestedCostCenter,
        string legacyGroup,
        string legacyRule)
    {
        return new BankReconciliationTransactionClassification
        {
            TypeKey = typeKey,
            TypeLabel = typeLabel,
            RuleKey = ruleKey,
            RuleLabel = ruleLabel,
            SuggestedAccount = suggestedAccount,
            SuggestedCostCenter = suggestedCostCenter,
            IsDefault = false,
            LegacyGroup = legacyGroup,
            LegacyRule = legacyRule
        };
    }

    private static BankReconciliationTransactionClassification CreateDefault()
        => new()
        {
            TypeKey = "def",
            TypeLabel = "DEF",
            RuleKey = "fallback",
            RuleLabel = "Standard",
            SuggestedAccount = null,
            SuggestedCostCenter = null,
            IsDefault = true,
            LegacyGroup = "Ovrigt",
            LegacyRule = "fallback"
        };

    private static bool IsMatch(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] keywords)
        => keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static string BuildNormalizedText(string? remittance, string? debtorName)
    {
        var parts = new[] { remittance, debtorName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeForSearch(value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return parts.Length == 0 ? string.Empty : string.Join(" ", parts);
    }

    private static string NormalizeForSearch(string? value)
    {
        var source = value ?? string.Empty;
        var normalized = source.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var current in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(current);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }

    private static string NormalizeRulePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "fallback" : value.Trim().ToUpperInvariant();
}
