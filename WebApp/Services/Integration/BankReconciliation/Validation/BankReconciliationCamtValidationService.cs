using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace WebApp.Services.Integration.BankReconciliation.Validation;

// Validates CAMT structure, totals and identifiers without making matching decisions.
public sealed class BankReconciliationCamtValidationService : IBankReconciliationCamtValidationService
{
    private static readonly Regex Camt053NamespaceRegex = new(
        @"^urn:iso:std:iso:20022:tech:xsd:camt\.053\.001\.\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly BankReconciliationCamtValidationOptions _options;

    public BankReconciliationCamtValidationService(IOptions<BankReconciliationCamtValidationOptions>? options = null)
    {
        _options = options?.Value ?? new BankReconciliationCamtValidationOptions();
    }

    public BankReconciliationCamtValidationResult Validate(string filePath)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = Math.Max(1, _options.MaximumXmlCharacters)
            };
            using var reader = XmlReader.Create(filePath, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return Invalid("invalid-xml", "Filen innehåller inte ett säkert och giltigt XML-dokument.");
        }

        var result = new BankReconciliationCamtValidationResult();
        var root = document.Root;
        var ns = root?.Name.Namespace ?? XNamespace.None;
        if (root?.Name.LocalName != "Document" || !Camt053NamespaceRegex.IsMatch(ns.NamespaceName))
        {
            AddError(result, "unsupported-document", "Filen är inte ett CAMT.053-kontoutdrag med en version som stöds.");
            return result;
        }

        result.CamtVersion = ns.NamespaceName.Split(':').LastOrDefault();

        var statements = document.Descendants(ns + "Stmt").ToList();
        result.StatementCount = statements.Count;
        result.TransactionCount = statements.Sum(statement => statement.Descendants(ns + "TxDtls").Count());
        if (statements.Count != 1)
        {
            AddError(result, "statement-count", "Filen måste innehålla exakt ett kontoutdrag.");
            return result;
        }

        ValidateStatement(statements[0], ns, result);
        return result;
    }

    private static void ValidateStatement(XElement statement, XNamespace ns, BankReconciliationCamtValidationResult result)
    {
        result.StatementId = NormalizeValue(statement.Element(ns + "Id")?.Value);
        RequireText(statement.Element(ns + "Id"), result, "missing-statement-id", "Kontoutdraget saknar statement-id.");

        var account = statement.Element(ns + "Acct");
        var accountId = account?.Element(ns + "Id");
        var hasAccountId = HasText(accountId?.Element(ns + "IBAN"))
            || HasText(accountId?.Element(ns + "Othr")?.Element(ns + "Id"));
        if (!hasAccountId)
            AddError(result, "missing-account", "Kontoutdraget saknar ett identifierbart bankkonto.");

        result.MaskedAccount = MaskAccount(
            accountId?.Element(ns + "IBAN")?.Value
            ?? accountId?.Element(ns + "Othr")?.Element(ns + "Id")?.Value);

        var accountCurrency = Normalize(account?.Element(ns + "Ccy")?.Value);
        result.Currency = accountCurrency;
        if (string.IsNullOrWhiteSpace(accountCurrency))
            AddError(result, "missing-account-currency", "Kontoutdraget saknar kontovaluta.");

        var entries = statement.Elements(ns + "Ntry").ToList();
        result.EntryCount = entries.Count;
        result.BookedEntryCount = entries.Count(entry => string.Equals(
            Normalize(entry.Element(ns + "Sts")?.Value),
            "BOOK",
            StringComparison.Ordinal));
        result.BlockedEntryCount = result.EntryCount - result.BookedEntryCount;
        if (entries.Count == 0)
            AddError(result, "missing-entries", "Kontoutdraget innehåller inga bokföringsposter.");

        var signedEntryTotal = 0m;
        var transactionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var serviceReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            signedEntryTotal += ValidateEntry(entry, ns, accountCurrency, transactionIds, serviceReferences, result);

        ValidateBalanceEquation(statement, ns, accountCurrency, signedEntryTotal, result);
    }

    private static decimal ValidateEntry(
        XElement entry,
        XNamespace ns,
        string? accountCurrency,
        ISet<string> transactionIds,
        ISet<string> serviceReferences,
        BankReconciliationCamtValidationResult result)
    {
        var status = Normalize(entry.Element(ns + "Sts")?.Value);
        if (!string.Equals(status, "BOOK", StringComparison.OrdinalIgnoreCase))
            AddError(result, "entry-not-booked", "Kontoutdraget innehåller en post som inte är bokförd.");

        var direction = Normalize(entry.Element(ns + "CdtDbtInd")?.Value);
        if (direction is not ("CRDT" or "DBIT"))
            AddError(result, "invalid-direction", "En bokföringspost har ogiltig kredit- eller debetriktning.");

        ValidateDate(entry.Element(ns + "BookgDt")?.Element(ns + "Dt"), result, "invalid-booking-date", "En bokföringspost saknar giltigt bokföringsdatum.");
        ValidateDate(entry.Element(ns + "ValDt")?.Element(ns + "Dt"), result, "invalid-value-date", "En bokföringspost saknar giltigt valutadatum.");

        var entryAmountNode = entry.Element(ns + "Amt");
        var entryAmount = ParseAmount(entryAmountNode, result, "invalid-entry-amount", "En bokföringspost saknar ett giltigt belopp.");
        ValidateCurrency(entryAmountNode, accountCurrency, result);

        var details = entry.Descendants(ns + "TxDtls").ToList();
        if (details.Count == 0)
            AddError(result, "missing-transaction-details", "En bokföringspost saknar transaktionsdetaljer.");

        var detailTotal = 0m;
        foreach (var detail in details)
            detailTotal += ValidateTransactionDetail(detail, ns, accountCurrency, transactionIds, serviceReferences, result);

        if (entryAmount.HasValue && entryAmount.Value != detailTotal)
            AddError(result, "entry-total-mismatch", "Summan av transaktionsdetaljerna stämmer inte med bokföringspostens belopp.");

        foreach (var entryDetails in entry.Elements(ns + "NtryDtls"))
        {
            var actualCount = entryDetails.Descendants(ns + "TxDtls").Count();
            foreach (var batch in entryDetails.Elements(ns + "Btch"))
            {
                var declaredCounts = batch.Elements(ns + "NbOfTxs").Select(node => ParseInteger(node.Value)).ToList();
                if (declaredCounts.Any(count => count is null) || declaredCounts.Sum(count => count ?? 0) != actualCount)
                    AddError(result, "batch-count-mismatch", "Batchens angivna transaktionsantal stämmer inte med innehållet.");
            }
        }

        if (!entryAmount.HasValue || direction is not ("CRDT" or "DBIT"))
            return 0m;

        return direction == "CRDT" ? entryAmount.Value : -entryAmount.Value;
    }

    private static decimal ValidateTransactionDetail(
        XElement detail,
        XNamespace ns,
        string? accountCurrency,
        ISet<string> transactionIds,
        ISet<string> serviceReferences,
        BankReconciliationCamtValidationResult result)
    {
        var amountNode = detail.Element(ns + "AmtDtls")?.Element(ns + "TxAmt")?.Element(ns + "Amt");
        var amount = ParseAmount(amountNode, result, "invalid-transaction-amount", "En transaktion saknar ett giltigt belopp.");
        ValidateCurrency(amountNode, accountCurrency, result);

        var refs = detail.Element(ns + "Refs");
        AddDuplicateWarning(refs?.Element(ns + "TxId")?.Value, transactionIds, result, "duplicate-transaction-id", "Filen innehåller återanvända TxId och kräver sammansatt dubblettkontroll.");
        AddDuplicateWarning(refs?.Element(ns + "AcctSvcrRef")?.Value, serviceReferences, result, "duplicate-service-reference", "Filen innehåller återanvända bankreferenser och kräver sammansatt dubblettkontroll.");

        var referredDocumentAmountNodes = detail.Element(ns + "RmtInf")?
            .Descendants(ns + "RfrdDocAmt")
            .ToList() ?? new List<XElement>();
        var remittedAmountNodes = referredDocumentAmountNodes
            .Select(node => node.Element(ns + "RmtdAmt"))
            .Where(node => node is not null)
            .Cast<XElement>()
            .ToList();
        var creditNoteAmountNodes = referredDocumentAmountNodes
            .Select(node => node.Element(ns + "CdtNoteAmt"))
            .Where(node => node is not null)
            .Cast<XElement>()
            .ToList();
        foreach (var allocationAmountNode in remittedAmountNodes.Concat(creditNoteAmountNodes))
            ValidateCurrency(allocationAmountNode, accountCurrency, result);

        var remittedAmounts = remittedAmountNodes.Select(ParseOptionalAmount).ToList();
        var creditNoteAmounts = creditNoteAmountNodes.Select(ParseOptionalAmount).ToList();
        if (remittedAmounts.Any(value => value is null) || creditNoteAmounts.Any(value => value is null))
        {
            AddError(result, "invalid-remitted-amount", "En strukturerad fakturaallokering innehåller ett ogiltigt belopp.");
        }
        else if (referredDocumentAmountNodes.Count > 0 && amount.HasValue &&
                 remittedAmounts.Sum(value => value ?? 0m) - creditNoteAmounts.Sum(value => value ?? 0m) != amount.Value)
        {
            AddError(result, "remitted-total-mismatch", "Fakturaallokeringarna summerar inte till transaktionsbeloppet.");
        }

        return amount ?? 0m;
    }

    private static void ValidateBalanceEquation(
        XElement statement,
        XNamespace ns,
        string? accountCurrency,
        decimal signedEntryTotal,
        BankReconciliationCamtValidationResult result)
    {
        var balances = statement.Elements(ns + "Bal")
            .Select(balance => new
            {
                Type = Normalize(balance.Element(ns + "Tp")?.Element(ns + "CdOrPrtry")?.Element(ns + "Cd")?.Value),
                AmountNode = balance.Element(ns + "Amt"),
                Direction = Normalize(balance.Element(ns + "CdtDbtInd")?.Value)
            })
            .Where(balance => balance.Type is "OPBD" or "CLBD")
            .ToList();

        var openingBalances = balances.Where(balance => balance.Type == "OPBD").ToList();
        var closingBalances = balances.Where(balance => balance.Type == "CLBD").ToList();
        if (openingBalances.Count != 1 || closingBalances.Count != 1)
        {
            AddError(result, "missing-booked-balances", "Kontoutdraget saknar bokförd öppnings- eller stängningsbalans.");
            return;
        }

        var opening = openingBalances[0];
        var closing = closingBalances[0];

        var openingAmount = ParseAmount(opening.AmountNode, result, "invalid-opening-balance", "Öppningsbalansen är ogiltig.");
        var closingAmount = ParseAmount(closing.AmountNode, result, "invalid-closing-balance", "Stängningsbalansen är ogiltig.");
        result.OpeningBalance = openingAmount;
        result.ClosingBalance = closingAmount;
        ValidateCurrency(opening.AmountNode, accountCurrency, result);
        ValidateCurrency(closing.AmountNode, accountCurrency, result);
        if (!openingAmount.HasValue || !closingAmount.HasValue)
            return;

        var signedOpening = ApplyDirection(openingAmount.Value, opening.Direction, result);
        var signedClosing = ApplyDirection(closingAmount.Value, closing.Direction, result);
        if (signedOpening + signedEntryTotal != signedClosing)
            AddError(result, "balance-equation-mismatch", "Öppningsbalans, bokföringsposter och stängningsbalans går inte ihop.");
    }

    private static decimal ApplyDirection(decimal amount, string? direction, BankReconciliationCamtValidationResult result)
    {
        if (direction == "CRDT") return amount;
        if (direction == "DBIT") return -amount;
        AddError(result, "invalid-balance-direction", "En balans har ogiltig kredit- eller debetriktning.");
        return 0m;
    }

    private static decimal? ParseAmount(XElement? node, BankReconciliationCamtValidationResult result, string code, string message)
    {
        var value = ParseOptionalAmount(node);
        if (!value.HasValue || value.Value < 0m)
            AddError(result, code, message);
        return value is >= 0m ? value : null;
    }

    private static decimal? ParseOptionalAmount(XElement? node)
        => decimal.TryParse(Normalize(node?.Value), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static int? ParseInteger(string? value)
        => int.TryParse(Normalize(value), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : null;

    private static void ValidateCurrency(XElement? amountNode, string? accountCurrency, BankReconciliationCamtValidationResult result)
    {
        var currency = Normalize(amountNode?.Attribute("Ccy")?.Value);
        if (string.IsNullOrWhiteSpace(currency) || !string.Equals(currency, accountCurrency, StringComparison.OrdinalIgnoreCase))
            AddError(result, "currency-mismatch", "Ett belopp saknar kontovalutan eller använder en annan valuta.");
    }

    private static void ValidateDate(XElement? node, BankReconciliationCamtValidationResult result, string code, string message)
    {
        if (!DateTime.TryParseExact(Normalize(node?.Value), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            AddError(result, code, message);
    }

    private static void AddDuplicateWarning(
        string? value,
        ISet<string> seen,
        BankReconciliationCamtValidationResult result,
        string code,
        string message)
    {
        var normalized = Normalize(value);
        if (!string.IsNullOrWhiteSpace(normalized) && !seen.Add(normalized)
            && result.Issues.All(issue => issue.Code != code))
        {
            result.Issues.Add(new BankReconciliationCamtValidationIssue
            {
                Code = code,
                Message = message,
                Severity = BankReconciliationCamtValidationSeverity.Warning
            });
        }
    }

    private static void RequireText(XElement? node, BankReconciliationCamtValidationResult result, string code, string message)
    {
        if (!HasText(node)) AddError(result, code, message);
    }

    private static bool HasText(XElement? node) => !string.IsNullOrWhiteSpace(node?.Value);
    private static string? NormalizeValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? MaskAccount(string? value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9]", string.Empty).ToUpperInvariant();
        if (normalized.Length == 0)
            return null;

        if (normalized.Length <= 8)
            return $"•••• {normalized[Math.Max(0, normalized.Length - 4)..]}";

        return $"{normalized[..4]} •••• {normalized[^4..]}";
    }

    private static void AddError(BankReconciliationCamtValidationResult result, string code, string message)
        => result.Issues.Add(new BankReconciliationCamtValidationIssue
        {
            Code = code,
            Message = message,
            Severity = BankReconciliationCamtValidationSeverity.Error
        });

    private static BankReconciliationCamtValidationResult Invalid(string code, string message)
    {
        var result = new BankReconciliationCamtValidationResult();
        AddError(result, code, message);
        return result;
    }

}
