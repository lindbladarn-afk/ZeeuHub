// Executes Jeeves SQL operations with safe dependency telemetry and consistent failures.
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Repository.Execution;

public sealed class JeevesSqlExecutor : IJeevesSqlExecutor
{
    private const int DefaultCommandTimeoutSeconds = 15;
    private const int SlowOperationThresholdMilliseconds = 2_000;
    private readonly ILogger<JeevesSqlExecutor> _logger;

    public JeevesSqlExecutor(ILogger<JeevesSqlExecutor> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<T> Query<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null)
    {
        return WithConnection(
            connectionString,
            connection => connection.Query<T>(sql, param, commandType: commandType, commandTimeout: commandTimeoutSeconds ?? DefaultCommandTimeoutSeconds).ToList(),
            operationName);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithConnectionAsync(
            connectionString,
            operationName,
            async connection =>
            {
                var rows = await connection.QueryAsync<T>(BuildCommand(sql, param, commandType, commandTimeoutSeconds, cancellationToken));
                return rows.ToList();
            });
    }

    public T? QueryFirstOrDefault<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null)
    {
        return WithConnection(
            connectionString,
            connection => connection.QueryFirstOrDefault<T>(sql, param, commandType: commandType, commandTimeout: commandTimeoutSeconds ?? DefaultCommandTimeoutSeconds),
            operationName);
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            connectionString,
            operationName,
            connection => connection.QueryFirstOrDefaultAsync<T>(BuildCommand(sql, param, commandType, commandTimeoutSeconds, cancellationToken)));
    }

    public Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            connectionString,
            operationName,
            connection => connection.ExecuteScalarAsync<T?>(BuildCommand(sql, param, commandType, commandTimeoutSeconds, cancellationToken)));
    }

    public int Execute(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null)
    {
        return WithConnection(
            connectionString,
            connection => connection.Execute(sql, param, commandType: commandType, commandTimeout: commandTimeoutSeconds ?? DefaultCommandTimeoutSeconds),
            operationName);
    }

    public Task<int> ExecuteAsync(
        string connectionString,
        string sql,
        object? param = null,
        CommandType commandType = CommandType.Text,
        string? operationName = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            connectionString,
            operationName,
            connection => connection.ExecuteAsync(BuildCommand(sql, param, commandType, commandTimeoutSeconds, cancellationToken)));
    }

    public T WithConnection<T>(
        string connectionString,
        Func<SqlConnection, T> action,
        string? operationName = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Jeeves connection string is required.", nameof(connectionString));
        ArgumentNullException.ThrowIfNull(action);

        var effectiveOperationName = NormalizeOperationName(operationName);
        var timer = Stopwatch.StartNew();
        var connectionOpened = false;

        using var activity = StartActivity(effectiveOperationName);
        using var scope = BeginOperationScope(effectiveOperationName);
        _logger.LogDebug(
            "Jeeves SQL operation started. {Operation} {ExternalSystem}",
            effectiveOperationName,
            "Jeeves");

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            connectionOpened = true;
            var result = action(connection);
            RecordSuccess(activity, effectiveOperationName, timer.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(activity, ex, effectiveOperationName, connectionOpened, timer.ElapsedMilliseconds);
            throw;
        }
    }

    public Task<TResult> WithConnectionAsync<TResult>(
        string connectionString,
        Func<SqlConnection, Task<TResult>> action,
        string? operationName = null)
    {
        return ExecuteWithConnectionAsync(connectionString, operationName, action);
    }

    private async Task<TResult> ExecuteWithConnectionAsync<TResult>(
        string connectionString,
        string? operationName,
        Func<SqlConnection, Task<TResult>> action)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Jeeves connection string is required.", nameof(connectionString));
        ArgumentNullException.ThrowIfNull(action);

        var effectiveOperationName = NormalizeOperationName(operationName);
        var timer = Stopwatch.StartNew();
        var connectionOpened = false;

        using var activity = StartActivity(effectiveOperationName);
        using var scope = BeginOperationScope(effectiveOperationName);
        _logger.LogDebug(
            "Jeeves SQL operation started. {Operation} {ExternalSystem}",
            effectiveOperationName,
            "Jeeves");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            connectionOpened = true;
            var result = await action(connection);
            RecordSuccess(activity, effectiveOperationName, timer.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(activity, ex, effectiveOperationName, connectionOpened, timer.ElapsedMilliseconds);
            throw;
        }
    }

    private IDisposable? BeginOperationScope(string operationName)
        => _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Module"] = "JeevesSql",
            ["Operation"] = operationName,
            ["ExternalSystem"] = "Jeeves"
        });

    private static Activity? StartActivity(string operationName)
    {
        var activity = JeevesSqlTelemetry.ActivitySource.StartActivity(
            "JeevesSql.Execute",
            ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation.name", operationName);
        return activity;
    }

    private void RecordSuccess(Activity? activity, string operationName, long durationMs)
    {
        activity?.SetTag("portal.result", "Succeeded");
        activity?.SetTag("portal.duration_ms", durationMs);
        activity?.SetStatus(ActivityStatusCode.Ok);

        if (durationMs >= SlowOperationThresholdMilliseconds)
        {
            _logger.LogWarning(
                "Jeeves SQL operation was slow. {ErrorCode} {Operation} {ExternalSystem} {DurationMs} {Result}",
                JeevesSqlTelemetry.SlowQueryErrorCode,
                operationName,
                "Jeeves",
                durationMs,
                "Succeeded");
            return;
        }

        _logger.LogDebug(
            "Jeeves SQL operation completed. {Operation} {ExternalSystem} {DurationMs} {Result}",
            operationName,
            "Jeeves",
            durationMs,
            "Succeeded");
    }

    private void RecordFailure(
        Activity? activity,
        Exception exception,
        string operationName,
        bool connectionOpened,
        long durationMs)
    {
        var failureKind = ClassifyFailure(exception);
        var errorCode = ResolveErrorCode(exception, connectionOpened);
        var sqlErrorNumber = exception is SqlException sqlException ? sqlException.Number : (int?)null;

        activity?.SetTag("portal.result", "Failed");
        activity?.SetTag("portal.error_code", errorCode);
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetTag("db.response.status_code", sqlErrorNumber);
        activity?.SetTag("portal.duration_ms", durationMs);
        activity?.SetStatus(ActivityStatusCode.Error);

        if (exception is OperationCanceledException)
        {
            _logger.LogInformation(
                "Jeeves SQL operation was cancelled. {Operation} {ExternalSystem} {DurationMs} {Result}",
                operationName,
                "Jeeves",
                durationMs,
                "Cancelled");
            return;
        }

        _logger.LogError(
            exception,
            "Jeeves SQL operation failed. {ErrorCode} {FailureKind} {Operation} {ExternalSystem} {DurationMs} {SqlErrorNumber} {Result}",
            errorCode,
            failureKind,
            operationName,
            "Jeeves",
            durationMs,
            sqlErrorNumber,
            "Failed");
    }

    private static string ResolveErrorCode(Exception exception, bool connectionOpened)
    {
        if (exception is SqlException { Number: -2 })
            return JeevesSqlTelemetry.QueryTimeoutErrorCode;

        return connectionOpened
            ? JeevesSqlTelemetry.QueryFailedErrorCode
            : JeevesSqlTelemetry.ConnectionFailedErrorCode;
    }

    private static string NormalizeOperationName(string? operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            return "UnnamedJeevesOperation";

        var trimmed = operationName.Trim();
        return trimmed.Length <= 128 ? trimmed : trimmed[..128];
    }

    private static CommandDefinition BuildCommand(
        string sql,
        object? param,
        CommandType commandType,
        int? commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        return new CommandDefinition(
            sql,
            param,
            commandType: commandType,
            commandTimeout: commandTimeoutSeconds ?? DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken);
    }

    internal static string ClassifyFailure(Exception ex)
    {
        if (ex is OperationCanceledException)
            return "Cancelled";

        if (ex is SqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                -2 => "Timeout",
                53 or 64 or 10054 or 233 => "ConnectionUnavailable",
                18456 => "Authentication",
                208 or 207 or 2812 => "SchemaOrProcedure",
                _ => "SqlError"
            };
        }

        return "Unexpected";
    }
}
