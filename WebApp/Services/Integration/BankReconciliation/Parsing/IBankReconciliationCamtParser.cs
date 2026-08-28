using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Parses CAMT files into normalized bank reconciliation transactions and source references.
public interface IBankReconciliationCamtParser
{
    IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath);

    BankReconciliationParsedDocument ParseDocument(string filePath)
    {
        var statement = new BankReconciliationParsedStatement();
        statement.Entries.Add(new BankReconciliationParsedEntry
        {
            Transactions = Parse(filePath).ToList()
        });
        return new BankReconciliationParsedDocument
        {
            Statements = [statement]
        };
    }
}
