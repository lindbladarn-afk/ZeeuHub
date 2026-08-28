using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Imports;

// Creates account, statement and document hashes used by the import registry.
internal static class BankReconciliationImportFingerprint
{
    public static string Account(BankReconciliationParsedStatement statement)
        => Hash(Canonical(statement.AccountIban, statement.AccountNumber, statement.AccountCurrency));

    public static string Statement(BankReconciliationParsedStatement statement)
        => Hash(Canonical(
            Account(statement),
            statement.StatementId));

    public static string Document(BankReconciliationParsedDocument document)
    {
        var statementParts = document.Statements
            .Select(StatementContent)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var transactionParts = document.Transactions
            .Select(transaction => transaction.DuplicateFingerprint)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return Hash(Canonical(string.Join('|', statementParts), string.Join('|', transactionParts)));
    }

    private static string StatementContent(BankReconciliationParsedStatement statement)
    {
        var balances = statement.Balances
            .Select(balance => Canonical(
                balance.TypeCode,
                balance.Amount.ToString("0.############################", CultureInfo.InvariantCulture),
                balance.Currency,
                balance.Direction,
                balance.Date))
            .OrderBy(value => value, StringComparer.Ordinal);
        return Hash(Canonical(
            Statement(statement),
            statement.ElectronicSequenceNumber,
            statement.LegalSequenceNumber,
            statement.CreatedAt,
            string.Join('|', balances)));
    }

    private static string Canonical(params string?[] values)
        => string.Join('\u001f', values.Select(value => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant()));

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
