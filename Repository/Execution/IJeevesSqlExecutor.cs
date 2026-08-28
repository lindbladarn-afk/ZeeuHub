namespace Repository.Execution;

// Shared execution layer for tenant-scoped Jeeves SQL calls.
// It centralizes connection opening, command timeout defaults, and error classification/logging.
public interface IJeevesSqlExecutor
{
    IReadOnlyList<T> Query<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null);

    Task<IReadOnlyList<T>> QueryAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    T? QueryFirstOrDefault<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null);

    Task<T?> QueryFirstOrDefaultAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    int Execute(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null);

    Task<int> ExecuteAsync(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default);

    T WithConnection<T>(
        string connectionString,
        Func<SqlConnection, T> action,
        string? operationName = null);

    Task<TResult> WithConnectionAsync<TResult>(
        string connectionString,
        Func<SqlConnection, Task<TResult>> action,
        string? operationName = null);
}
