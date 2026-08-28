using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Creates anonymous, repeatable transaction identities from stable CAMT source fields.
internal static class BankReconciliationCamtTransactionIdentity
{
    public static string CreateBaseFingerprint(BankReconciliationParsedTransaction transaction)
    {
        var canonical = string.Join('\u001f', new[]
        {
            transaction.StatementAccountIban,
            transaction.StatementAccountNumber,
            transaction.StatementId,
            transaction.EntryReference,
            transaction.EntryAccountServiceReference,
            transaction.BatchMessageId,
            transaction.BatchPaymentInformationId,
            transaction.InstructionId,
            transaction.PaymentInformationId,
            transaction.EndToEndId,
            transaction.TxId,
            transaction.AcctSvcrRef,
            transaction.Date,
            transaction.ValueDate,
            transaction.Direction,
            transaction.Amount.ToString("0.############################", CultureInfo.InvariantCulture),
            transaction.Currency,
            transaction.Reference,
            transaction.Remittance,
            transaction.DebtorOrganizationId,
            transaction.DebtorAccountId,
            transaction.CreditorOrganizationId,
            transaction.CreditorAccountId
        }.Select(Normalize));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string CreateDuplicateFingerprint(BankReconciliationParsedTransaction transaction)
    {
        var canonical = string.Join('\u001f', new[]
        {
            transaction.StatementAccountIban,
            transaction.StatementAccountNumber,
            transaction.InstructionId,
            transaction.EndToEndId,
            transaction.TxId,
            transaction.AcctSvcrRef,
            transaction.Date,
            transaction.ValueDate,
            transaction.Direction,
            transaction.Amount.ToString("0.############################", CultureInfo.InvariantCulture),
            transaction.Currency,
            transaction.Reference,
            transaction.Remittance,
            transaction.DebtorOrganizationId,
            transaction.DebtorAccountId,
            transaction.CreditorOrganizationId,
            transaction.CreditorAccountId
        }.Select(Normalize));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static void Assign(BankReconciliationParsedTransaction transaction, string baseFingerprint, int occurrence)
    {
        var uniqueSource = string.Concat(baseFingerprint, ":", occurrence.ToString(CultureInfo.InvariantCulture));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uniqueSource)));
        transaction.SourceFingerprint = fingerprint;
        transaction.DuplicateFingerprint = CreateDuplicateFingerprint(transaction);
        transaction.Id = $"TX-{fingerprint[..24]}";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}
