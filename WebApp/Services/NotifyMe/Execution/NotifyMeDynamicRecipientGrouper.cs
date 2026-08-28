namespace WebApp.Services.NotifyMe;

// Groups dynamic NotifyMe result rows by the original Mottagare column.
public static class NotifyMeDynamicRecipientGrouper
{
    public const string RecipientColumnName = "Mottagare";

    public static IReadOnlyList<NotifyMeRecipientBatch> GroupByRecipient(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
            return Array.Empty<NotifyMeRecipientBatch>();

        var recipientColumn = rows[0].Keys.FirstOrDefault(x =>
            string.Equals(x, RecipientColumnName, StringComparison.OrdinalIgnoreCase));
        if (recipientColumn is null)
            throw new InvalidOperationException("Dynamiska mottagare kräver kolumnen Mottagare i SQL-resultatet.");

        var groupedRows = new List<(string Recipient, List<IReadOnlyDictionary<string, object?>> Rows)>();
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var recipient = NormalizeRecipient(row.TryGetValue(recipientColumn, out var value) ? value : null);
            if (recipient.Length == 0)
                throw new InvalidOperationException("Dynamiska mottagare kräver att varje träff har värde i kolumnen Mottagare.");

            if (!indexes.TryGetValue(recipient, out var batchIndex))
            {
                batchIndex = groupedRows.Count;
                indexes[recipient] = batchIndex;
                groupedRows.Add((recipient, new List<IReadOnlyDictionary<string, object?>>()));
            }

            groupedRows[batchIndex].Rows.Add(row);
        }

        return groupedRows
            .Select(x => new NotifyMeRecipientBatch(x.Recipient, x.Rows))
            .ToArray();
    }

    private static string NormalizeRecipient(object? value)
    {
        return value?.ToString()?.Trim() ?? string.Empty;
    }
}

public sealed record NotifyMeRecipientBatch(
    string Recipient,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
