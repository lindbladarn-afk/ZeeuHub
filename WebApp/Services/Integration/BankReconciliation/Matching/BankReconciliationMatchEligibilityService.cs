using System.Globalization;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Validates booking status, accounting direction, currency and source data before matching is allowed.
public sealed class BankReconciliationMatchEligibilityService : IBankReconciliationMatchEligibilityService
{
    private const string Passed = "passed";
    private const string Warning = "warning";
    private const string Blocked = "blocked";

    public BankReconciliationMatchEligibilityResult Evaluate(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(invoice);

        var result = new BankReconciliationMatchEligibilityResult();
        AddRule(result, "booking-status", IsBooked(transaction),
            "Transaktionen är bokförd (BOOK).",
            "Endast bokförda transaktioner med status BOOK får matchas.");

        var expectedDirection = invoice.IsSupplierInvoice ? "DBIT" : "CRDT";
        var directionIsValid = string.Equals(transaction.Direction?.Trim(), expectedDirection, StringComparison.OrdinalIgnoreCase)
            && (invoice.IsSupplierInvoice ? transaction.Amount < 0m : transaction.Amount > 0m);
        AddRule(result, "accounting-direction", directionIsValid,
            invoice.IsSupplierInvoice
                ? "Debettransaktionen avser en leverantörsfaktura."
                : "Kredittransaktionen avser en kundfaktura.",
            invoice.IsSupplierInvoice
                ? "Leverantörsfakturor kräver DBIT och ett negativt transaktionsbelopp."
                : "Kundfakturor kräver CRDT och ett positivt transaktionsbelopp.");

        AddRule(result, "non-zero-amount", transaction.Amount != 0m,
            "Transaktionsbeloppet är skilt från noll.",
            "Transaktioner med nollbelopp får inte matchas.");

        var transactionCurrency = NormalizeCurrency(transaction.Currency);
        var invoiceCurrency = NormalizeCurrency(invoice.Currency);
        var currenciesAreValid = IsCurrencyCode(transactionCurrency)
            && IsCurrencyCode(invoiceCurrency)
            && string.Equals(transactionCurrency, invoiceCurrency, StringComparison.Ordinal);
        AddRule(result, "currency", currenciesAreValid,
            $"Valutan stämmer ({transactionCurrency}).",
            "Transaktion och faktura måste ha samma giltiga ISO-valuta.");

        var dateIsValid = TryParseDate(transaction.Date, out _) || TryParseDate(transaction.ValueDate, out _);
        AddRule(result, "transaction-date", dateIsValid,
            "Transaktionen har ett giltigt bokförings- eller valutadatum.",
            "Transaktionen saknar ett giltigt datum i formatet yyyy-MM-dd.");

        if (LooksLikeNumericOcr(invoice.Ocr) && !HasValidOcrCheckDigit(invoice.Ocr))
        {
            result.Rules.Add(new BankReconciliationEligibilityRule
            {
                Code = "ocr-check-digit",
                Status = Warning,
                Message = "Fakturans numeriska OCR har en ogiltig kontrollsiffra och kräver manuell granskning."
            });
        }
        else if (!string.IsNullOrWhiteSpace(invoice.Ocr))
        {
            result.Rules.Add(new BankReconciliationEligibilityRule
            {
                Code = "ocr-check-digit",
                Status = Passed,
                Message = "OCR-formatet är godkänt för matchning."
            });
        }

        return result;
    }

    private static bool IsBooked(BankReconciliationTransactionCandidate transaction)
        => string.Equals(transaction.EntryStatus?.Trim(), "BOOK", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? currency)
        => (currency ?? string.Empty).Trim().ToUpperInvariant();

    private static bool IsCurrencyCode(string currency)
        => currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z');

    private static bool TryParseDate(string? value, out DateTime date)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool LooksLikeNumericOcr(string? value)
    {
        var compact = CompactDigits(value);
        return compact.Length >= 2 && compact.All(char.IsDigit);
    }

    private static bool HasValidOcrCheckDigit(string? value)
    {
        var digits = CompactDigits(value);
        if (digits.Length < 2 || !digits.All(char.IsDigit))
            return false;

        var sum = 0;
        var factor = 2;
        for (var index = digits.Length - 2; index >= 0; index--)
        {
            var product = (digits[index] - '0') * factor;
            sum += product > 9 ? product - 9 : product;
            factor = factor == 2 ? 1 : 2;
        }

        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    private static string CompactDigits(string? value)
        => new((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray());

    private static void AddRule(
        BankReconciliationMatchEligibilityResult result,
        string code,
        bool passed,
        string passedMessage,
        string blockedMessage)
    {
        result.Rules.Add(new BankReconciliationEligibilityRule
        {
            Code = code,
            Status = passed ? Passed : Blocked,
            Message = passed ? passedMessage : blockedMessage
        });
    }
}
