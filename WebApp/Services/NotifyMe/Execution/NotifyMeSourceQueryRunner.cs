using Microsoft.Data.SqlClient;

namespace WebApp.Services.NotifyMe;

public interface INotifyMeSourceQueryRunner
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default);
}

// Executes the rule-owned source SQL against the active tenant Jeeves database.
public sealed class NotifyMeSourceQueryRunner : INotifyMeSourceQueryRunner
{
    private const int CommandTimeoutSeconds = 60;

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var sourceConnection = new SqlConnection(connectionString);
        await sourceConnection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, sourceConnection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);

            rows.Add(row);
        }

        return rows;
    }
}
