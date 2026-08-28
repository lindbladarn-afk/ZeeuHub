using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Calculates remaining transaction and invoice balances without mutating source data.
public static class BankReconciliationAllocationBalance
{
    public static IReadOnlyList<BankReconciliationTransactionCandidate> BuildAvailableTransactions(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<BankReconciliationSavedMatch> matches)
    {
        var allocatedByTransaction = matches
            .GroupBy(match => match.TransactionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(match => match.MatchedAmount), StringComparer.OrdinalIgnoreCase);

        return transactions
            .Select(transaction => CloneWithRemainingAmount(transaction, allocatedByTransaction))
            .Where(transaction => Math.Abs(transaction.Amount) > 0m)
            .ToList();
    }

    public static IReadOnlyList<InvoiceItem> BuildAvailableInvoices(
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyList<BankReconciliationSavedMatch> matches)
    {
        var allocatedByInvoice = matches
            .GroupBy(match => match.InvoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(match => match.MatchedAmount), StringComparer.OrdinalIgnoreCase);

        return invoices
            .Select(invoice => CloneWithRemainingAmount(invoice, allocatedByInvoice))
            .Where(invoice => invoice.RemainingAmount > 0m)
            .ToList();
    }

    private static BankReconciliationTransactionCandidate CloneWithRemainingAmount(
        BankReconciliationTransactionCandidate source,
        IReadOnlyDictionary<string, decimal> allocatedByTransaction)
    {
        var allocated = allocatedByTransaction.TryGetValue(source.TransactionId, out var amount) ? amount : 0m;
        var remaining = Math.Max(Math.Abs(source.Amount) - allocated, 0m);
        var signedRemaining = source.Amount < 0m ? -remaining : remaining;

        return new BankReconciliationTransactionCandidate
        {
            TransactionId = source.TransactionId,
            StatementId = source.StatementId,
            StatementAccountIban = source.StatementAccountIban,
            StatementAccountNumber = source.StatementAccountNumber,
            StatementAccountOwner = source.StatementAccountOwner,
            StatementBankBic = source.StatementBankBic,
            Date = source.Date,
            ValueDate = source.ValueDate,
            EntryStatus = source.EntryStatus,
            Direction = source.Direction,
            Amount = signedRemaining,
            Currency = source.Currency,
            Reference = source.Reference,
            ReferenceType = source.ReferenceType,
            EndToEndId = source.EndToEndId,
            TransactionIdSource = source.TransactionIdSource,
            AccountServiceReference = source.AccountServiceReference,
            DebtorName = source.DebtorName,
            Remittance = source.Remittance,
            ReferenceCandidates = source.ReferenceCandidates.ToList(),
            ResolvedCodingTypeKey = source.ResolvedCodingTypeKey,
            ResolvedCodingTypeLabel = source.ResolvedCodingTypeLabel,
            ResolvedCodingAccount = source.ResolvedCodingAccount,
            ResolvedCodingCostCenter = source.ResolvedCodingCostCenter,
            ResolvedCodingIsDefault = source.ResolvedCodingIsDefault
        };
    }

    private static InvoiceItem CloneWithRemainingAmount(
        InvoiceItem source,
        IReadOnlyDictionary<string, decimal> allocatedByInvoice)
    {
        var allocated = allocatedByInvoice.TryGetValue(source.InvoiceNo, out var amount) ? amount : 0m;
        return new InvoiceItem
        {
            InvoiceNo = source.InvoiceNo,
            Customer = source.Customer,
            SalesPerson = source.SalesPerson,
            DueDate = source.DueDate,
            PaidDate = source.PaidDate,
            AmountSek = source.AmountSek,
            AmountExclVat = source.AmountExclVat,
            PaidAmount = source.PaidAmount,
            RemainingAmount = Math.Max(source.RemainingAmount - allocated, 0m),
            Ocr = source.Ocr,
            Currency = source.Currency,
            CompanyCode = source.CompanyCode,
            IsSupplierInvoice = source.IsSupplierInvoice,
            IsPaid = source.IsPaid,
            Status = source.Status
        };
    }
}
